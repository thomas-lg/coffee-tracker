import { request } from '@playwright/test';
import { writeFile, mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';
import { E2E_PASSWORD, uniqueEmail, ADMIN_STATE_FILE, type ProvisionedUser } from './session';

/**
 * Claims the instance's first account and reopens registration for the suite.
 *
 * A fresh instance accepts registrations only until its first account exists, then
 * closes them by itself. Without this, exactly one test could register and every
 * other would get a 403, and with tests running in parallel, *which* one won the
 * race would decide it. Doing it once, before anything else runs, makes the suite
 * independent of ordering.
 *
 * The account is also the instance's administrator (first user wins), which is what
 * lets it reopen registration at all.
 */
export default async function globalSetup(): Promise<void> {
  const api = await request.newContext({ baseURL: 'http://localhost:5000' });

  const email = uniqueEmail('suite-admin');
  const registered = await api.post('/api/auth/register', {
    data: { email, password: E2E_PASSWORD, displayName: 'Suite Admin' },
  });

  if (!registered.ok()) {
    throw new Error(
      `e2e global setup could not claim the first account (${registered.status()}). ` +
        'The API is expected to be running on :5000 against an empty database.',
    );
  }

  const admin = (await registered.json()) as ProvisionedUser & { isAdmin: boolean };
  if (!admin.isAdmin) {
    // Registering worked but did not yield an administrator, so an account already
    // existed: the database is not fresh. CI always starts from an empty one; locally
    // this means a previous run's data is still there.
    throw new Error(
      'e2e expects an empty database. The account it just created is not the administrator, ' +
        "so one already existed. Delete the API's database file and start it again.",
    );
  }

  // Reopen what registering just closed. Deliberate rather than bootstrap-opened, so
  // it stays open for the rest of the run.
  const reopened = await api.put('/api/admin/settings', {
    headers: { authorization: `Bearer ${admin.token}` },
    data: { localLoginEnabled: true, localRegistrationEnabled: true },
  });
  if (!reopened.ok()) {
    throw new Error(`e2e global setup could not reopen registration (${reopened.status()}).`);
  }

  await mkdir(dirname(ADMIN_STATE_FILE), { recursive: true });
  await writeFile(ADMIN_STATE_FILE, JSON.stringify({ ...admin, email }, null, 2));
  await api.dispose();
}
