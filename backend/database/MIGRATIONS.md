# Migrations Pipeline (T-004)

EF Core Migrations (Backend Stack Q8, Locked) on PostgreSQL. This document
covers the *migrations* mechanism only. Roles and extensions stay in the
T-003 bootstrap (`bootstrap/*.sql`) and are **not** repeated in any EF
migration.

## Order of operations for a database

1. T-003 bootstrap, once per database, by an administrator
   (`bootstrap/001..003`, see `README.md`). Creates `app_migrator`,
   `app_runtime`, `app_reports`, the extensions and the default privileges.
2. Migrations, every deploy, by CI/CD, connecting **as `app_migrator`**.

## Creating a migration (developers)

```
dotnet tool restore                      # pins dotnet-ef (.config/dotnet-tools.json)
npm run migrations:add -- <Name>         # or the dotnet ef command below
npm run migrations:script                # idempotent SQL, for the reviewer
```
Equivalent direct command:
```
dotnet ef migrations add <Name> --project backend/src/SmartClinic.Infrastructure --startup-project backend/src/SmartClinic.Migrator --output-dir Persistence/Migrations
```
The design-time factory points at a placeholder database, so no server is
needed to create a migration and `dotnet ef database update` cannot
silently reach a real database.

Commit all three files EF generates (`<id>_<Name>.cs`, `.Designer.cs`, and
the updated `*ModelSnapshot.cs`); `scripts/check-migration-guards.mjs`
fails if the set is incomplete.

## Applying migrations (CI/CD only)

```
SMARTCLINIC_MIGRATOR_CONNECTION_STRING=<from the pipeline secret store>
dotnet run --project backend/src/SmartClinic.Migrator
```
`MigrationApplier` refuses to run unless the connection authenticates as
`app_migrator`. The running API never migrates.

After migrating, it revokes INSERT/UPDATE/DELETE on `__EFMigrationsHistory`
from `app_runtime` and `app_reports` (T-003's default privileges would
otherwise grant them). SELECT stays as T-003 grants it. T-003's SQL is not
changed; this is done by the pipeline that creates that table.

## Verification

- `npm run check` — Node-only guards (no runtime migrate calls, no committed
  credentials, complete migration set, pinned tooling). No Docker/.NET needed.
- `npm run verify:backend` — `dotnet build` + `dotnet test`. The tests start a
  disposable PostgreSQL Testcontainer, apply the real T-003 SQL, apply the
  migrations as `app_migrator`, and check history, idempotency, the identity
  guard and that `app_runtime`/`app_reports` have no DDL path. **Requires a
  Docker daemon; without one the tests fail (they never skip).**

## Required outside this repository (not implementable here)

The CI/CD host is still open (OD-ARCH-03), so none of this is configured:

- The CI job must run `npm run ci` (or the same steps) and be a **required**
  status check; apply-migrations must run only after it passes
  (Technical Plan: test -> build image -> apply migrations -> deploy).
- Branch protection that requires human code review of every migration
  (e.g. review required on `backend/src/**/Persistence/Migrations/**`).
- Docker available on the CI runner.
- `app_migrator` credentials held only by the deployment identity (secret
  manager, Q34); no human/shared access to production credentials.
- Production DB access controls: repo code cannot stop someone who holds
  superuser or `app_migrator` credentials from changing the database.
