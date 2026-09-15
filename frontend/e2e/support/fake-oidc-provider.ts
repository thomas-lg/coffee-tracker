import { createServer, type Server } from 'node:http';
import { generateKeyPairSync, createSign, randomUUID } from 'node:crypto';
import { AddressInfo } from 'node:net';

/**
 * A minimal, standards-shaped OpenID Connect provider for the e2e suite.
 *
 * Why a fake rather than a test account on a real one: the API must be started
 * pointing at the issuer before any browser opens, CI cannot reach a private
 * provider, and a shared account would make these tests fail whenever someone
 * else's infrastructure hiccups. None of the bugs this guards against were
 * provider-specific, they were all in our own callback handling, so a compliant
 * stand-in exercises exactly the same paths, deterministically.
 *
 * It implements only what the flow touches: discovery, JWKS, the authorization
 * endpoint (which redirects straight back with a code, no login screen) and the
 * token endpoint.
 */
export interface FakeProviderUser {
  sub: string;
  email: string;
  emailVerified: boolean;
  name: string;
  groups: string[];
}

export interface FakeProvider {
  issuer: string;
  clientId: string;
  /** Who the next authorization will sign in. Change it between tests. */
  setUser(user: FakeProviderUser): void;
  /** The ID token minted for the last exchange, lets a test replay it. */
  lastIdToken(): string | null;
  close(): Promise<void>;
}

const CLIENT_ID = 'coffee-tracker-e2e';

export async function startFakeProvider(user: FakeProviderUser): Promise<FakeProvider> {
  const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  const jwk = publicKey.export({ format: 'jwk' }) as { n: string; e: string };
  const kid = 'e2e-key-1';

  let currentUser = user;
  let issuer = '';
  let lastIdToken: string | null = null;
  // code -> nonce, so the ID token carries back what the client asked for. The
  // library validates the nonce itself; getting it wrong fails the sign-in exactly
  // as a real provider would.
  const codes = new Map<string, string>();

  const server: Server = createServer((req, res) => {
    const url = new URL(req.url ?? '/', issuer);

    if (url.pathname === '/.well-known/openid-configuration') {
      return json(res, {
        issuer,
        authorization_endpoint: `${issuer}/authorize`,
        token_endpoint: `${issuer}/token`,
        jwks_uri: `${issuer}/jwks`,
        response_types_supported: ['code'],
        subject_types_supported: ['public'],
        id_token_signing_alg_values_supported: ['RS256'],
        scopes_supported: ['openid', 'profile', 'email', 'groups'],
        code_challenge_methods_supported: ['S256'],
      });
    }

    if (url.pathname === '/jwks') {
      return json(res, {
        keys: [{ kty: 'RSA', use: 'sig', alg: 'RS256', kid, n: jwk.n, e: jwk.e }],
      });
    }

    // No login screen: a provider that always says yes keeps the test about our
    // callback handling rather than about typing a password.
    if (url.pathname === '/authorize') {
      const redirectUri = url.searchParams.get('redirect_uri') ?? '/';
      const state = url.searchParams.get('state') ?? '';
      const code = randomUUID();
      codes.set(code, url.searchParams.get('nonce') ?? '');
      const back = new URL(redirectUri);
      back.searchParams.set('code', code);
      back.searchParams.set('state', state);
      back.searchParams.set('iss', issuer);
      res.writeHead(302, { location: back.toString(), 'access-control-allow-origin': '*' });
      return res.end();
    }

    if (url.pathname === '/token' && req.method === 'POST') {
      let body = '';
      req.on('data', (c) => (body += c));
      req.on('end', () => {
        const code = new URLSearchParams(body).get('code') ?? '';
        const nonce = codes.get(code);
        // Authorization codes are single-use at a real provider; mirroring that keeps
        // a replayed code from quietly working here.
        codes.delete(code);
        if (nonce === undefined) {
          return json(res, { error: 'invalid_grant' }, 400);
        }
        lastIdToken = signIdToken({ privateKey, kid, issuer, user: currentUser, nonce });
        return json(res, {
          access_token: 'e2e-access-token',
          id_token: lastIdToken,
          token_type: 'Bearer',
          expires_in: 300,
        });
      });
      return;
    }

    res.writeHead(404).end();
  });

  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  issuer = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;

  return {
    issuer,
    clientId: CLIENT_ID,
    setUser: (u) => (currentUser = u),
    lastIdToken: () => lastIdToken,
    close: () => new Promise<void>((resolve) => server.close(() => resolve())),
  };
}

function signIdToken(o: {
  privateKey: import('node:crypto').KeyObject;
  kid: string;
  issuer: string;
  user: FakeProviderUser;
  nonce: string;
}): string {
  const now = Math.floor(Date.now() / 1000);
  const header = { alg: 'RS256', typ: 'JWT', kid: o.kid };
  const payload = {
    iss: o.issuer,
    aud: CLIENT_ID,
    sub: o.user.sub,
    exp: now + 300,
    iat: now,
    nonce: o.nonce,
    email: o.user.email,
    email_verified: o.user.emailVerified,
    name: o.user.name,
    groups: o.user.groups,
  };
  const signingInput = `${b64(header)}.${b64(payload)}`;
  const signature = createSign('RSA-SHA256').update(signingInput).sign(o.privateKey);
  return `${signingInput}.${base64url(signature)}`;
}

const b64 = (o: unknown) => base64url(Buffer.from(JSON.stringify(o)));
const base64url = (b: Buffer) =>
  b.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

function json(res: import('node:http').ServerResponse, body: unknown, status = 200): void {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    'content-type': 'application/json',
    'content-length': Buffer.byteLength(payload),
    // The SPA exchanges the code from the browser, so the token endpoint has to
    // answer cross-origin, the very thing that was missing on the real provider.
    'access-control-allow-origin': '*',
  });
  res.end(payload);
}
