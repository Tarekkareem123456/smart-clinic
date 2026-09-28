# Smart Clinic — Repository Foundation (T-001)

This is the initial repository/project structure only. No business or
clinical functionality is implemented. See `T-001-report.md` for the full
acceptance-criteria evidence.

## Layout

```
apps/
  staff/      Staff application surface (placeholder)
  portal/     Patient Portal application surface (placeholder, isolated from staff)
packages/
  shared/     Frontend contracts shared by apps/* (depends on nothing under apps/)
    src/i18n/   T-008 — ar/en foundation: Locale/Direction types, dir/lang
                application on <html>, and the client-side "translate a
                server-sent error code" mechanism (Technical Plan §6.4).
backend/
  SmartClinic.sln
  src/
    SmartClinic.Domain/           no project references (depends on nothing)
    SmartClinic.Application/      -> Domain
    SmartClinic.Infrastructure/   -> Domain, Application
    SmartClinic.Api/              -> Application
  database/                      T-003 — roles + baseline extensions
                                  (separate from the future EF Core
                                  Migrations pipeline, T-004). See
                                  backend/database/README.md.
scripts/
  check-boundaries.mjs    frontend boundary enforcement (no Portal -> Staff imports)
  check-rtl-ltr.mjs       T-008 — fails on hard-coded left/right (Backlog TR-14)
  check-sql-structure.mjs T-003 — basic structural sanity check on the new .sql files
```

## Verifying the boundaries

Frontend, source-level check (no install required, plain Node):

```
node scripts/check-boundaries.mjs
```

Frontend, real build/bundle (no install required — uses the TypeScript
compiler's own `outFile` mode as a bundler; no Vite/webpack/esbuild needed):

```
cd apps/portal
npx tsc -p tsconfig.build.json
grep -i "staff" dist/portal.bundle.js   # must find nothing
```

Frontend, T-008 hard-coded left/right rule (no install required, plain Node):

```
node scripts/check-rtl-ltr.mjs
```

Backend:

```
cd backend
dotnet build
```

Database (T-003 — requires a real PostgreSQL instance, not available in
this sandbox; see backend/database/README.md and the T-003 report):

```
cd backend/database
psql -v ON_ERROR_STOP=1 -f bootstrap/001_roles.sql              <conn args>
psql -v ON_ERROR_STOP=1 -f bootstrap/002_extensions.sql         <conn args>
psql -v ON_ERROR_STOP=1 -f bootstrap/003_default_privileges.sql <conn args>
psql -v ON_ERROR_STOP=1 -f verify/verify_t003.sql               <conn args>
```

Domain-depends-on-nothing and most other layer-direction rules are enforced
by the project reference graph itself: any forbidden reverse reference
(e.g. Domain -> Application) creates an MSBuild circular project reference
and fails `dotnet build`/`dotnet restore`. The one gap that plain circular-
reference detection cannot catch — `Infrastructure -> Api` — is closed
explicitly by `backend/Directory.Build.targets`, a pure-MSBuild target that
fails the build if that specific reference is ever added. Details and exact
verification commands are in the T-001 report / AC-3+AC-5 addendum.
