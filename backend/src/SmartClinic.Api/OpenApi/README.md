# OpenAPI Pipeline — Staff / Portal separation (T-005)

One ASP.NET Core API (`SmartClinic.Api`), two OpenAPI documents. There is
no second API project and no hand-maintained endpoint list anywhere —
separation is a single rule in `SurfaceDocumentInclusion.cs`.

## The mechanism

`SurfaceDocumentInclusion.ResolveGroup(explicitGroupName, relativePath)`:

1. If the endpoint sets an explicit group — `[ApiExplorerSettings(GroupName = "portal")]`
   on a controller, or `.WithGroupName("portal")` on a minimal-API route —
   that wins.
2. Otherwise: `"portal"` if the route starts with `api/v1/portal/`,
   `"staff"` for everything else.

This is wired into Swashbuckle's `DocInclusionPredicate` in
`OpenApiServiceCollectionExtensions.cs`. A new endpoint needs **zero**
extra configuration to land in the right document — it just needs to be
mapped under the right path (matching the route table Architecture
already documents), or explicitly opt out via `WithGroupName`.

`Endpoints/PipelineProbeEndpoints.cs` are **not business endpoints** —
placeholders proving the mechanism, same role as T-001's placeholder
`main.ts` files and T-004's empty Baseline migration. Delete them once
real endpoints exist.

## Generating the contracts (CI)

```
dotnet tool restore
dotnet build backend/SmartClinic.sln -c Release
npm run openapi:generate        # writes artifacts/openapi/{staff,portal}.json
```
Deterministic: `dotnet swagger tofile` reads the built assembly directly
(no live server, no network, no random ports), so the same commit always
produces byte-identical output. `artifacts/` is git-ignored — these are
build outputs, not source.

## Generating the typed clients

```
npm run generate:clients         # openapi-typescript, pinned to major v6
```
Writes `apps/staff/src/generated/staff-client.ts` and
`apps/portal/src/generated/portal-client.ts` — **never hand-edited**.
Each app only ever gets its **own** client; Portal's build never sees
Staff's (same isolation T-001 already proved for the rest of the bundle).

`openapi-typescript` could not actually be run in the environment this was
built in (no network to fetch it) — see the T-005 report. The two files
above currently contain a clearly-labeled placeholder, not real generator
output.

## TR-11 — generated clients live behind services/*

`apps/*/src/services/*` is the only place allowed to import
`apps/*/src/generated/*`. `scripts/check-client-usage.mjs` fails the build
if any hook/component/feature file imports a generated client directly.

## What's outside this repository (not implementable here)

- No CI workflow file was added: the CI/CD host is still open
  (OD-ARCH-03). Whatever host is eventually chosen needs to run
  `npm run check`, `dotnet build`/`dotnet test`, and `npm run openapi:generate`
  + `npm run generate:clients`, and fail if `git diff` shows the generated
  files changed but weren't committed (contract-drift check) — a policy
  decision for that host's config, not something to script here.
- `.NET SDK`/Docker/network were unavailable in the environment this was
  authored in; none of the `dotnet`/`npm run openapi:*`/`npm run generate:*`
  commands above were actually executed — see the T-005 report for exactly
  what was and wasn't run.
