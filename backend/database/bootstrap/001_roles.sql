-- T-003 — Database: Roles and Baseline Extensions
-- 001_roles.sql
--
-- Creates the three PostgreSQL roles required by the Backend Stack Final
-- Summary / Technical Plan baseline. Idempotent: safe to run more than
-- once against the same database/cluster.
--
-- Roles:
--   app_migrator — owns/creates schema objects. Used only by the future
--                  EF Core Migrations pipeline (T-004, Backend Stack Final
--                  Summary Q8: "Entity Framework Core Migrations — Locked").
--                  Never used by the running API.
--   app_runtime  — the API's normal runtime connection. Gets standard CRUD
--                  on tables app_migrator creates, via default privileges
--                  (003_default_privileges.sql). Does NOT get UPDATE/DELETE
--                  on the Audit table specifically — see
--                  ../templates/audit_table_privileges.sql.template and
--                  the T-003 report for why that can't be applied yet.
--   app_reports  — read-only connection (Dapper reporting path per the
--                  locked backend stack). Gets SELECT only, nothing else.
--
-- Passwords are deliberately NOT set here. Set them separately per
-- environment from your secrets manager, e.g.:
--   ALTER ROLE app_migrator WITH PASSWORD '...';
--   ALTER ROLE app_runtime  WITH PASSWORD '...';
--   ALTER ROLE app_reports  WITH PASSWORD '...';
-- Never commit real credentials to this repository.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'app_migrator') THEN
    EXECUTE format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'app_migrator');
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'app_runtime') THEN
    EXECUTE format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'app_runtime');
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'app_reports') THEN
    EXECUTE format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'app_reports');
  END IF;
END
$$;

-- Every role must be able to connect to the target database. Uses
-- current_database() dynamically rather than hard-coding a database name,
-- since no database name is specified in the frozen documentation.
--
-- Scope note: this only ADDS CONNECT for the three roles above. It does
-- NOT touch PUBLIC's existing CONNECT privilege (an earlier draft
-- revoked CONNECT from PUBLIC as a "hygiene" side effect; that has been
-- removed — T-003 is roles/extensions/table-privileges only and must not
-- change database-level connectivity for PUBLIC, which is a broader,
-- unrelated effect outside this task's scope).
DO $$
BEGIN
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO app_migrator, app_runtime, app_reports', current_database());
END
$$;
