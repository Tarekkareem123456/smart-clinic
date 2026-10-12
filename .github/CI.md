# CI pipeline (T-010)

Workflow: `.github/workflows/ci.yml`. Runs on every pull request, every push to
`master`, and on demand (`workflow_dispatch`). Every job calls commands the
repository already has; the only new tools are the two mandated scanners.

| Job | Covers | Reuses |
|---|---|---|
| **Static checks** | lint/static guards + typecheck of `packages/shared` | `npm run check` (T-001 boundaries, T-008 RTL/LTR rule, T-003 SQL structure, T-004 migration guards, T-005 client usage/tooling); `tsc -p packages/shared` |
| **Backend build + unit tests** | whole-solution Release build (backend typecheck, T-001 layer rule) + in-process tests | `dotnet build backend/SmartClinic.sln -c Release`; `SmartClinic.Api.Tests` |
| **Backend integration tests (Testcontainers)** | real PostgreSQL, real T-003 SQL, real migrations | `SmartClinic.Migrations.Tests` (T-004). Needs Docker; fails, never skips, without it |
| **Image build + Trivy** | builds the API image, then Trivy on the image and on the repository (vulnerabilities + Dockerfile misconfiguration) | `backend/Dockerfile`; fails on any HIGH/CRITICAL finding, including ones with no available fix |
| **gitleaks** | secret scan of the full git history | `.gitleaks.toml` (default rules + one narrow allowlist) |
| **CI gate** | the single required check; fails unless every job above succeeded; records timings | — |

## Merge gate (required status check)

The workflow cannot block a merge by itself; the host has to be told to. In the
repository settings (branch protection or a ruleset for `master`):

1. Require a pull request before merging.
2. Require status checks to pass, and add exactly one: **`CI gate`**.
3. (Recommended) require branches to be up to date before merging.

`CI gate` fails if any job failed, was skipped, or was cancelled, so a single
required check covers all of them. This setting lives on the host, not in git,
so it is not verified by anything in this repository.

## Pinned tools

GitHub actions are pinned to commit SHAs. Trivy and gitleaks are downloaded as
release binaries and verified against a SHA-256 taken from the publisher's own
checksum file (`TRIVY_*` / `GITLEAKS_*` in the workflow `env`). To upgrade,
change the version and the checksum together.

Implementation-time CI/tooling parameters chosen for T-010. None of them is
specified by the Backlog, the Architecture or the Backend Stack summary, and
none is an Architecture Open Decision; each is easy to change here:

- **Node 22.**
- **Trivy severity threshold `HIGH,CRITICAL`.**
- **Trivy is run without `--ignore-unfixed`.** Findings that have no available
  fix therefore also fail the Trivy steps.
- **Job timeouts of 15–30 min.**

The Backlog requires only that Trivy is a mandatory pipeline step (T-010
Acceptance Criteria); it does not define a threshold or unfixed-finding
handling.

.NET `8.0.x` follows the existing repository target `net8.0`
(`Directory.Build.props`). It is not a newly locked architecture decision.

## Running the same checks locally

```
npm run check
npx -p typescript@5 tsc -p packages/shared
dotnet build backend/SmartClinic.sln -c Release
dotnet test backend/tests/SmartClinic.Api.Tests -c Release --no-build
dotnet test backend/tests/SmartClinic.Migrations.Tests -c Release     # needs Docker
docker build -f backend/Dockerfile -t smartclinic-api:ci backend
gitleaks git --redact .
```

## Timing baseline

Each run's `CI gate` job writes per-job and total wall-clock seconds to the run
summary and uploads `ci-timing.json` (artifact `ci-timing`).

| Date | Commit | Pipeline wall-clock (s) | Notes |
|---|---|---|---|
| 2026-10-12 | 4a926e1 | 44 | First successful run; all five jobs succeeded (static 15s, backend 32s, integration 35s, image/Trivy 43s, gitleaks 5s). |

## Blocked / not covered (see the T-010 report)

- **Typecheck of `apps/staff` and `apps/portal`, and the portal-bundle staff-leak
  check.** They fail on the current code for reasons that need a decision, not a
  CI change: `typescript ^5` is declared but `apps/portal/tsconfig.build.json`
  uses `ignoreDeprecations: "6.0"` (invalid on TS 5); the T-005 services import
  `StaffApiPaths` / `PortalApiPaths`, which the generated clients do not export;
  and the apps import `@smart-clinic/shared` through workspace linking that the
  repo does not install.
- **Frontend unit tests.** No frontend test framework has been chosen (same open
  question as T-009), so none is run.
- **Lint beyond `npm run check`.** No other linter is approved, so none is added.
- **Image scope.** One API image only (the worker runs in-process, ADR-010; the
  separate-worker note E4 is undecided). Nothing is pushed or deployed.
