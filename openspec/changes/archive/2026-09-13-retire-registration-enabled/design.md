## Context

Two pieces of code answer the question *"what is this instance's account policy before
anyone set one?"*:

| | instance with users, no row |
|---|---|
| `AccountPolicySeeder.SeedAsync` | `hasUsers ? legacyRegistrationEnabled : true` |
| `EfAccountPolicy.DefaultForUnseededInstanceAsync` | `!hasUsers` → closed |

They agree on a fresh instance and disagree on a populated one. The disagreement exists
only because the seeder also honours a legacy environment variable. Removing that input
collapses both to one rule.

## Goals / Non-Goals

**Goals**

- One statement of the seeding rule, in one place.
- No deployment loses sign-in.
- The fresh-install bootstrap — register once, door shuts behind you — is untouched.

**Non-Goals**

- Changing what an administrator can set at runtime.
- Preserving the ability to configure registration from the environment. That is the thing
  being removed, deliberately.

## Decisions

### A populated instance seeds registration **closed**

The alternative is seeding it open, which is indefensible here. The asymmetry:

- **Closed** — the worst case is an operator who had set `REGISTRATION_ENABLED=true` and
  upgrades without crossing the bridge. They can still sign in (`LocalLoginEnabled` is
  seeded `true` unconditionally), the state is visible in the admin view, and one toggle
  fixes it. It also matches the value the app has always shipped as its default
  (`appsettings.json` was `false`), so an operator who never set the variable sees no change
  at all.
- **Open** — an instance the README describes as internet-exposed silently starts accepting
  public registration, with nothing prompting anyone to look. It contradicts the stance the
  README already states ("an instance left on the internet is never sitting open by
  accident") and the bootstrap rule that shuts the door after the first account.

A closed door you can see and open beats an open door you cannot see.

This is also not a new rule being invented: it is the answer `EfAccountPolicy` already gives
for the same instance state.

### The two registration flags stay separate assignments

After the change both `LocalRegistrationEnabled` and `RegistrationOpenedForBootstrap` are
`!hasUsers`. They are *not* the same thing and must not be collapsed into one local: they
diverge the moment an administrator opens registration — `LocalRegistrationEnabled` true,
`RegistrationOpenedForBootstrap` false — which is exactly what stops a deliberately-opened
door from shutting itself after the next account.

### No startup warning for a leftover variable

Three lines would catch an operator who left it in their Unraid template. Declined: it keeps
a read of the key alive, so the retirement would not be one; it is a maintenance item nobody
ever deletes; and ASP.NET already ignores unknown keys silently. The README deletion and the
release note are the right channel.

## Risks / Trade-offs

**An instance upgrading without crossing the bridge seeds registration closed.** Accepted.
Bounded to deployments that have never started on a build ≥ 2026-09-11, recoverable with one
toggle, and never affects sign-in.

**The upgrade path keeps a single automated guard.** After this change,
`AccountPolicyTests.An_instance_that_already_has_users_is_seeded_closed` is the only
assertion anywhere about `hasUsers == true && no settings row` — no integration test covers
it. Its comment says so, so that a later refactor does not quietly delete it.

## Migration Plan

1. Before merging, start the deployment once on the current image so the seeder consumes the
   legacy value while it still honours it, and confirm **Admin → Account settings** reads
   what is intended. After that the row is authoritative and the variable is already dead.
2. Remove `REGISTRATION_ENABLED` from the deployment's environment and restart. Nothing
   should change — that is the empirical proof the retirement is safe for this instance.
3. Merge. `release.yml` publishes `:latest` on every green push to `main`, so the order
   matters.

## Open Questions

None.
