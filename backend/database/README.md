# Database Bootstrap (T-003)

Roles and baseline extensions, required before any table exists. This is
deliberately **separate** from the future EF Core Migrations pipeline
(T-004, Backend Stack Final Summary Q8 — "Entity Framework Core
Migrations — Locked"):

- Roles are cluster/database-level objects, not part of EF Core's
  model-tracked schema. Creating them typically needs a superuser (or a
  role with `CREATEROLE`), which the app's own migration connection
  should not need day-to-day.
- `CREATE EXTENSION` similarly usually needs elevated privileges the
  ordinary migration role shouldn't hold long-term.
- None of this depends on any Domain/Application model existing, so it
  can run before EF Core has anything to migrate.

This split (a one-time, idempotent "bootstrap" step, separate from
ongoing versioned schema migrations) is a common, standard pattern for
exactly this reason — it is an implementation-detail choice, not a change
to any frozen architecture decision, and is flagged as such in the T-003
report for confirmation.

## Layout

```
bootstrap/
  001_roles.sql               app_migrator / app_runtime / app_reports
  002_extensions.sql          pg_trgm, btree_gist
  003_default_privileges.sql  the actual CRUD / read-only rule, applied
                               automatically to every table app_migrator
                               creates from now on
verify/
  verify_t003.sql             automated pass/fail checks (see below)
templates/
  audit_table_privileges.sql.template
                               NOT applied by T-003. A ready-to-use
                               one-liner for whichever task creates the
                               real Audit table (T-103 in the current
                               Backlog) — see that file and the T-003
                               report for why it can't be applied yet.
```

## Running it

Against any PostgreSQL 13+ instance, connected as a superuser (or a role
with `CREATEROLE`/`CREATEDB`-equivalent rights) on the target database:

```
psql -v ON_ERROR_STOP=1 -f bootstrap/001_roles.sql              <conn args>
psql -v ON_ERROR_STOP=1 -f bootstrap/002_extensions.sql         <conn args>
psql -v ON_ERROR_STOP=1 -f bootstrap/003_default_privileges.sql <conn args>
psql -v ON_ERROR_STOP=1 -f verify/verify_t003.sql               <conn args>
```

All four files are idempotent — safe to re-run against a database that
already has them applied (this repo's environment could not run a real
PostgreSQL instance to prove that empirically — see the T-003 report — so
please run these commands once, then a second time back-to-back, and
confirm the second run also exits 0 with no errors).

Passwords for the three roles are intentionally not set anywhere in this
repository. Set them per environment from your secrets manager:
```sql
ALTER ROLE app_migrator WITH PASSWORD '...';
ALTER ROLE app_runtime  WITH PASSWORD '...';
ALTER ROLE app_reports  WITH PASSWORD '...';
```

## What `verify_t003.sql` actually proves

- All three roles exist.
- Both extensions are installed, and `pg_trgm`'s `similarity()` function
  actually works (not just "the extension is listed").
- The default-privilege rule is correct for an ordinary future table:
  `app_runtime` gets full CRUD, `app_reports` gets SELECT only — proven
  against a **disposable probe table** created and rolled back inside a
  transaction, not any real business table.
- The exact REVOKE statement documented in
  `templates/audit_table_privileges.sql.template` — the one T-103 will
  need to run against the real Audit table — is proven to work correctly
  (yields insert-only for `app_runtime`) against that same disposable
  probe table.

That last point is the honest answer to Backlog AC-1 ("`app_runtime` has
no UPDATE/DELETE on the Audit table"): the real Audit table doesn't exist
yet (it's T-103's job to create it — see the T-003 report), so the rule
can't be applied to it yet, but the exact mechanism that will make it true
the moment that table exists has been written and proven here.
