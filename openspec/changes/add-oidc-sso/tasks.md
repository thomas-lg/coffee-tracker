## 1. Local-account policy — storage and enforcement

- [x] 1.1 Add an `AppSettings` single-row entity (`LocalLoginEnabled`, `LocalRegistrationEnabled`) and an EF migration creating its table.
- [x] 1.2 Seed the row on startup when absent: on an instance with users, sign-in enabled and registration from the legacy `REGISTRATION_ENABLED`; on an empty instance, both enabled.
- [x] 1.3 Add an `IAccountPolicy` driven port (read + update both settings) in `CoffeeTracker.Application/Ports/Driven` and its EF adapter in `CoffeeTracker.Infrastructure`.
- [x] 1.4 Gate `AuthService.RegisterAsync` on the registration setting, keeping the existing `AuthStatus.RegistrationDisabled` outcome, and close registration once the first account exists.
- [x] 1.5 Gate `AuthService.LoginAsync` on the sign-in setting with a new `AuthStatus.LocalLoginDisabled`, distinct from invalid credentials.
- [x] 1.6 Delete `IRegistrationPolicy`, `RegistrationPolicy`, `RegistrationOptions` and `IAuthService.RegistrationEnabled`, now unused.
- [x] 1.7 Tests: registration and login each refused by their own setting; the two settings are independent; a fresh instance registers its first account with no configuration and closes registration afterwards; an upgraded instance keeps its posture; the legacy variable is read once.

## 2. Local-account policy — admin surface

- [ ] 2.1 Add `GET` and `PUT /api/admin/settings` behind the existing `AuthorizationPolicies.Admin`.
- [ ] 2.2 Implement the lock-out guard on the sign-in setting: refuse a disable with `409` and an explanatory message unless an admin holds a login row for the configured issuer; never refuse a re-enable; registration carries no such condition.
- [ ] 2.3 Extend `ConfigDto` and `GET /api/config` with local sign-in, local registration and provider-availability flags.
- [ ] 2.4 Tests: non-admin is refused; disabling sign-in is refused without a proven provider admin and allowed with one; re-enable always allowed; disabling registration is never blocked; `/api/config` reflects all three flags.

## 3. OIDC — configuration and discovery

- [ ] 3.1 Add an `OidcOptions` record (authority, client id, scopes, admin claim, admin claim value) bound from configuration.
- [ ] 3.2 Register the OIDC services only when authority and client id are both present; fail fast at startup when exactly one is set, mirroring the `Jwt:Key` check.
- [ ] 3.3 Resolve the provider's discovery document and JWKS lazily, cached, so a slow or unreachable provider degrades to "unavailable" instead of blocking startup.
- [ ] 3.4 Tests: absent configuration leaves the feature dormant; partial configuration prevents startup; unreachable discovery still starts and reports unavailable.

## 4. OIDC — sign-in

- [ ] 4.1 Add an `IExternalSignIn` driving port and the `POST /api/auth/oidc` endpoint, rate-limited under the existing auth policy.
- [ ] 4.2 Validate the posted ID token: signature against the JWKS, issuer, audience equal to the client id, expiry, and the nonce bound to the sign-in request.
- [ ] 4.3 Implement link-or-create over `UserManager.FindByLoginAsync` / `AddLoginAsync`, keyed on issuer + `sub`, with the verified-email linking rule and the refusal on an unverified match.
- [ ] 4.4 Apply the admin claim mapping on every sign-in — granting and revoking — with the first-user bootstrap as the fallback when no mapping is configured.
- [ ] 4.5 Return the existing `AuthResponseDto` by reusing `TokenService` and the refresh-token issuance, so nothing downstream changes.
- [ ] 4.6 Tests: valid token yields a session; wrong issuer, wrong audience, expired, and replayed tokens are each refused; returning user matches on `sub` after an email change; verified-email link; unverified-match refusal; claim grants and revokes admin; bootstrap applies only without a mapping.

## 5. Web client — sign-in

- [ ] 5.1 Add `angular-auth-oidc-client` pinned to an exact 22.x version, configured for Authorization Code + PKCE against the discovered provider.
- [ ] 5.2 Read the three config flags at bootstrap and render the provider action, the local form, and the register link accordingly.
- [ ] 5.3 Run the provider flow and exchange its ID token at `POST /api/auth/oidc`, storing the resulting session through the existing `AuthStore` path.
- [ ] 5.4 Surface a refused provider sign-in on the sign-in screen without storing a session.
- [ ] 5.5 Tests: provider action hidden when unconfigured; local form hidden when local sign-in is disabled; register link hidden when registration is closed; session persists across reload after provider sign-in; refusal is shown.

## 6. Web client — admin control

- [ ] 6.1 Add the admin settings screen with both local-account controls shown together, reachable only by admins, alongside the existing photo-cleanup screen.
- [ ] 6.2 Apply changes through the API and reflect the stored values; render the `409` refusal inline and leave the sign-in control enabled.
- [ ] 6.3 Tests: non-admin is redirected and sees no navigation entry; toggling persists; the refusal is explained.

## 7. Documentation and release

- [ ] 7.1 Document the optional OIDC variables in the README, with a worked example for one provider and a note that any OIDC-compliant provider works, plus the removal of `REGISTRATION_ENABLED` and the self-closing first-account bootstrap.
- [ ] 7.2 Note the breaking change and the seeding behaviour in the deployment docs.
- [ ] 7.3 Run the full backend and frontend test suites and the production image build before proposing the change for review.
