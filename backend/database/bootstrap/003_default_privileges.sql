-- T-003 — Database: Roles and Baseline Extensions
-- 003_default_privileges.sql
--
-- Establishes the privilege baseline for objects that DON'T EXIST YET.
-- Nothing here touches an existing table, because no application table
-- exists at T-003 time (Affected Models: None, per Backlog). Every
-- CREATE TABLE that the future EF Core Migrations pipeline (T-004) runs
-- AS app_migrator will automatically pick up these grants the moment it's
-- created — no per-migration grant statements needed for the common case.
--
-- Schema: the existing `public` schema is used (no dedicated schema name
-- is specified anywhere in the frozen documentation, so none is invented
-- here). This is an implementation-detail default, not an architecture
-- decision — flagged in the T-003 report.
--
-- Idempotent: GRANT/REVOKE/ALTER DEFAULT PRIVILEGES are all safe to re-run.

-- Basic schema access. Everyone needs USAGE to see objects in it at all;
-- only app_migrator gets CREATE, since it's the only role that ever
-- creates schema objects (via the migrations pipeline).
GRANT USAGE ON SCHEMA public TO app_migrator, app_runtime, app_reports;
GRANT CREATE ON SCHEMA public TO app_migrator;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- ---------------------------------------------------------------------
-- app_runtime — the API's normal runtime role.
-- Standard CRUD on every table app_migrator creates from now on, plus
-- USAGE/SELECT on sequences (needed for identity/auto-increment columns).
--
-- NOTE — Audit table exception: this default deliberately does NOT special
-- -case any table name (Postgres default privileges cannot target a
-- specific future table name in advance; the table doesn't exist yet — see
-- the T-003 report). Per Architecture §14 ("AuditLogEntry rows are
-- insert-only at the database permission level... no UPDATE/DELETE grant
-- to the application's normal runtime role"), whichever task creates the
-- Audit table (T-103 per the current Backlog) MUST additionally run the
-- REVOKE in ../templates/audit_table_privileges.sql.template against that
-- table, on top of this default. This file alone is not sufficient for
-- the Audit table; it is sufficient for every other table.
-- ---------------------------------------------------------------------
ALTER DEFAULT PRIVILEGES FOR ROLE app_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE app_migrator IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO app_runtime;

-- ---------------------------------------------------------------------
-- app_reports — read-only. SELECT on tables only. No sequence privileges
-- (a read-only role never inserts, so it never needs nextval()/currval()).
-- ---------------------------------------------------------------------
ALTER DEFAULT PRIVILEGES FOR ROLE app_migrator IN SCHEMA public
  GRANT SELECT ON TABLES TO app_reports;
