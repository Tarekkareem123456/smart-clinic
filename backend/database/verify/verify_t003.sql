-- T-003 — Database: Roles and Baseline Extensions
-- verify_t003.sql
--
-- Automated verification (Backlog AC-6: "Provide automated verification
-- where practical"). Run with:
--   psql -v ON_ERROR_STOP=1 -f verify_t003.sql <connection args>
-- Any RAISE EXCEPTION below makes psql exit non-zero, so this is a real
-- pass/fail check suitable for a CI step, not just a manual eyeball.
--
-- Sections:
--   1. Roles exist.
--   2. Extensions installed + a real pg_trgm functional query.
--   3. Default-privilege behavior, proven against a disposable probe
--      table created and destroyed inside a rolled-back transaction — NOT
--      the real Audit table (which does not exist yet; see T-003 report).

-- =====================================================================
-- 1. Roles exist
-- =====================================================================
DO $$
DECLARE
  missing text;
BEGIN
  SELECT string_agg(r, ', ') INTO missing
  FROM unnest(ARRAY['app_migrator', 'app_runtime', 'app_reports']) r
  WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = r);

  IF missing IS NOT NULL THEN
    RAISE EXCEPTION 'T-003 verification FAILED: missing role(s): %', missing;
  END IF;

  RAISE NOTICE 'T-003 verification: all three roles exist (app_migrator, app_runtime, app_reports). PASS';
END
$$;

-- =====================================================================
-- 2. Extensions installed + functional pg_trgm query
-- =====================================================================
DO $$
DECLARE
  missing text;
BEGIN
  SELECT string_agg(e, ', ') INTO missing
  FROM unnest(ARRAY['pg_trgm', 'btree_gist']) e
  WHERE NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = e);

  IF missing IS NOT NULL THEN
    RAISE EXCEPTION 'T-003 verification FAILED: missing extension(s): %', missing;
  END IF;

  RAISE NOTICE 'T-003 verification: pg_trgm and btree_gist are both installed. PASS';
END
$$;

DO $$
DECLARE
  sim real;
BEGIN
  -- A real pg_trgm functional call (Backlog AC-3: "verified with a basic
  -- query"), not just a catalog check. Two similar-but-not-identical
  -- strings must score strictly between 0 and 1.
  SELECT similarity('Ahmed Hassan', 'Ahmad Hasan') INTO sim;

  IF sim IS NULL OR sim <= 0 OR sim >= 1 THEN
    RAISE EXCEPTION 'T-003 verification FAILED: pg_trgm similarity() returned unexpected value: %', sim;
  END IF;

  RAISE NOTICE 'T-003 verification: pg_trgm similarity(''Ahmed Hassan'', ''Ahmad Hasan'') = %. PASS', sim;
END
$$;

-- =====================================================================
-- 3. Default-privilege behavior, proven against a disposable probe table
--    (never the real Audit table — that doesn't exist yet, see report)
-- =====================================================================
BEGIN;

SET ROLE app_migrator;
CREATE TABLE _t003_verification_probe (id int);
RESET ROLE;

DO $$
BEGIN
  -- 3a. Ordinary future table: app_runtime should get full CRUD by default.
  IF NOT has_table_privilege('app_runtime', '_t003_verification_probe', 'SELECT') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_runtime lacks default SELECT on a new table.';
  END IF;
  IF NOT has_table_privilege('app_runtime', '_t003_verification_probe', 'INSERT') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_runtime lacks default INSERT on a new table.';
  END IF;
  IF NOT has_table_privilege('app_runtime', '_t003_verification_probe', 'UPDATE') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_runtime lacks default UPDATE on a new (non-Audit) table.';
  END IF;
  IF NOT has_table_privilege('app_runtime', '_t003_verification_probe', 'DELETE') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_runtime lacks default DELETE on a new (non-Audit) table.';
  END IF;

  -- 3b. app_reports: read-only. SELECT yes, everything else no.
  IF NOT has_table_privilege('app_reports', '_t003_verification_probe', 'SELECT') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_reports lacks default SELECT on a new table.';
  END IF;
  IF has_table_privilege('app_reports', '_t003_verification_probe', 'INSERT') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_reports unexpectedly has INSERT — it must be read-only.';
  END IF;
  IF has_table_privilege('app_reports', '_t003_verification_probe', 'UPDATE') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_reports unexpectedly has UPDATE — it must be read-only.';
  END IF;
  IF has_table_privilege('app_reports', '_t003_verification_probe', 'DELETE') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_reports unexpectedly has DELETE — it must be read-only.';
  END IF;

  RAISE NOTICE 'T-003 verification: default privileges on an ordinary new table are correct (app_runtime=CRUD, app_reports=SELECT-only). PASS';
END
$$;

-- 3c. Simulate the Audit-table follow-up (the exact statement documented
-- in ../templates/audit_table_privileges.sql.template) against the probe
-- table, and prove it produces insert-only for app_runtime. This proves
-- the mechanism T-103 will need to apply to the *real* Audit table,
-- without creating or naming that real table here.
REVOKE UPDATE, DELETE ON _t003_verification_probe FROM app_runtime;

DO $$
BEGIN
  IF NOT has_table_privilege('app_runtime', '_t003_verification_probe', 'SELECT') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_runtime lost SELECT after the Audit-style REVOKE (should keep it).';
  END IF;
  IF NOT has_table_privilege('app_runtime', '_t003_verification_probe', 'INSERT') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_runtime lost INSERT after the Audit-style REVOKE (should keep it — insert-only).';
  END IF;
  IF has_table_privilege('app_runtime', '_t003_verification_probe', 'UPDATE') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_runtime still has UPDATE after the Audit-style REVOKE.';
  END IF;
  IF has_table_privilege('app_runtime', '_t003_verification_probe', 'DELETE') THEN
    RAISE EXCEPTION 'T-003 verification FAILED: app_runtime still has DELETE after the Audit-style REVOKE.';
  END IF;

  RAISE NOTICE 'T-003 verification: the Audit-table REVOKE template correctly yields insert-only for app_runtime (SELECT+INSERT, no UPDATE/DELETE) when applied. PASS';
END
$$;

-- Discard the probe table and the simulated revoke — nothing persists.
ROLLBACK;

DO $$
BEGIN
  RAISE NOTICE 'T-003 verification: all checks passed.';
END
$$;
