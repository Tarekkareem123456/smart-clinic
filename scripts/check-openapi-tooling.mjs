#!/usr/bin/env node
/**
 * T-005 — Swashbuckle version-consistency guard.
 *
 * Same idea as scripts/check-migration-guards.mjs's dotnet-ef check: the
 * OpenAPI generation mechanism spans three places (the Api package
 * reference, the Api.Tests package reference used to read the generated
 * documents in tests, and the CLI tool manifest used to write staff.json /
 * portal.json in CI) — they must all be the exact same Swashbuckle
 * version, or "deterministic" (T-005 requirement) breaks silently.
 */

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { join } from "node:path";

const ROOT = fileURLToPath(new URL("..", import.meta.url));

const API_CSPROJ = join(ROOT, "backend/src/SmartClinic.Api/SmartClinic.Api.csproj");
const TESTS_CSPROJ = join(ROOT, "backend/tests/SmartClinic.Api.Tests/SmartClinic.Api.Tests.csproj");
const TOOLS_JSON = join(ROOT, ".config/dotnet-tools.json");

function swashbuckleVersion(csprojPath) {
  const content = readFileSync(csprojPath, "utf8");
  const match = content.match(/Include="Swashbuckle\.AspNetCore"\s+Version="([^"]+)"/);
  return match ? match[1] : null;
}

const problems = [];

const apiVersion = swashbuckleVersion(API_CSPROJ);
const testsVersion = swashbuckleVersion(TESTS_CSPROJ);

if (!apiVersion) problems.push(`${API_CSPROJ}: no Swashbuckle.AspNetCore PackageReference found`);
if (!testsVersion) problems.push(`${TESTS_CSPROJ}: no Swashbuckle.AspNetCore PackageReference found`);
if (apiVersion && testsVersion && apiVersion !== testsVersion) {
  problems.push(`Swashbuckle.AspNetCore version mismatch: Api=${apiVersion} vs Api.Tests=${testsVersion}`);
}

let cliVersion = null;
try {
  const tools = JSON.parse(readFileSync(TOOLS_JSON, "utf8"));
  cliVersion = tools.tools?.["swashbuckle.aspnetcore.cli"]?.version ?? null;
} catch (e) {
  problems.push(`${TOOLS_JSON}: could not read/parse (${e.message})`);
}

if (!cliVersion) {
  problems.push(`${TOOLS_JSON}: swashbuckle.aspnetcore.cli is not declared as a local tool`);
} else if (apiVersion && cliVersion !== apiVersion) {
  problems.push(`swashbuckle.aspnetcore.cli ${cliVersion} != Swashbuckle.AspNetCore ${apiVersion} (Api package)`);
}

if (problems.length > 0) {
  console.error("OpenAPI tooling guard(s) failed:\n");
  for (const p of problems) console.error(`  ${p}`);
  console.error(`\n${problems.length} problem(s). Failing.`);
  process.exit(1);
}

console.log(`OpenAPI tooling guard passed: Swashbuckle pinned consistently at ${apiVersion} everywhere.`);
process.exit(0);
