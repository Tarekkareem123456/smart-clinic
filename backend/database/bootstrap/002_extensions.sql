-- T-003 — Database: Roles and Baseline Extensions
-- 002_extensions.sql
--
-- Enables the two candidate extensions named in Technical Plan §"Extensions
-- المحتملة" and Backlog T-003. Idempotent (IF NOT EXISTS).
--
--   pg_trgm     — trigram similarity search. Enabled and verified now
--                 (Backlog T-003 AC-3), in preparation for T-101 Arabic
--                 Search Spike, which will decide *how* it's actually used
--                 for patient search (FR-SEARCH-001).
--
--   btree_gist  — enabled now as the baseline candidate extension.
--                 IMPORTANT: enabling it is NOT the same as using it.
--                 Its intended use — an EXCLUDE USING gist constraint on
--                 Appointment as defense-in-depth against double-booking —
--                 is explicitly conditioned on OD-007 (Technical Plan
--                 rows "Extensions المحتملة" and "Double-booking": "[A]
--                 مشروط" / "Guidance مشروط بـ OD-007"), which is still
--                 open. T-003 does not create any exclusion constraint and
--                 does not assume how OD-007 will be resolved — it only
--                 makes the capability available so that whichever task
--                 eventually implements Appointment (after OD-007 is
--                 decided) can use it without a separate extension-install
--                 step.

CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS btree_gist;
