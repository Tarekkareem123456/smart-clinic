#!/usr/bin/env node
/**
 * T-004 — repo-level guards for the controlled migrations mechanism
 * (Backlog T-004 / Technical Plan TR-66: migrations are versioned,
 * reviewed, applied only through CI/CD, no manual DB edits).
 *
 * Runs with plain Node (no .NET SDK, no Docker needed) so it can gate any
 * CI host cheaply. It complements — does not replace — the Testcontainers
 * tests, which exercise the real database behaviour.
 *
 * Guards:
 *  1. Schema changes are never applied from runtime code: Migrate /
 *     MigrateAsync / EnsureCreated / MigrationApplier may appear only in
 *     the Migrator project and MigrationApplier.cs itself.
 *  2. No credentials are committed under backend/src (Password= / Pwd=).
 *  3. Every migration is a complete, consistent triple: <id>_<Name>.cs +
 *     <id>_<Name>.Designer.cs (with a matching [Migration("<id>_<Name>")])
 *     and a ModelSnapshot exists — so reviewers always see the full change.
 *  4. The dotnet-ef tool is pinned to an exact version that matches the
 *     Microsoft.EntityFrameworkCore.Design version in the Migrator.
 */

import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = fileURLToPath(new URL("..", import.meta.url));
const SRC = join(ROOT, "backend/src");
const MIGRATIONS_DIR = join(SRC, "SmartClinic.Infrastructure/Persistence/Migrations");
const problems = [];
const rel = (p) => relative(ROOT, p).split(sep).join("/");

function walk(dir, predicate) {
  let out = [];
  let entries = [];
  try {
    entries = readdirSync(dir);
  } catch {
    return out;
  }
  for (const entry of entries) {
    if (entry === "bin" || entry === "obj" || entry === "node_modules") continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) out = out.concat(walk(full, predicate));
    else if (predicate(full)) out.push(full);
  }
  return out;
}

// --- 1. No migration/schema-creation calls from runtime code -------------
const ALLOWED_MIGRATE_FILES = new Set([
  "backend/src/SmartClinic.Infrastructure/Persistence/MigrationApplier.cs",
]);
const isAllowedForMigrate = (file) => {
  const r = rel(file);
  return (
    ALLOWED_MIGRATE_FILES.has(r) ||
    r.startsWith("backend/src/SmartClinic.Migrator/") ||
    r.startsWith("backend/src/SmartClinic.Infrastructure/Persistence/Migrations/")
  );
};
const FORBIDDEN_RUNTIME = /\.Migrate\s*\(|\.MigrateAsync\s*\(|EnsureCreated(Async)?\s*\(|\bMigrationApplier\b/;

for (const file of walk(SRC, (f) => f.endsWith(".cs"))) {
  if (isAllowedForMigrate(file)) continue;
  readFileSync(file, "utf8")
    .split("\n")
    .forEach((line, i) => {
      if (line.trim().startsWith("//")) return; // comments/docs may mention the applier
      if (FORBIDDEN_RUNTIME.test(line)) {
        problems.push(
          `${rel(file)}:${i + 1} schema change/migration call outside the controlled mechanism: "${line.trim()}"`
        );
      }
    });
}

// --- 2. No committed credentials under backend/src ------------------------
const CREDENTIAL = /\b(Password|Pwd)\s*=/i;
for (const file of walk(SRC, (f) => /\.(cs|json|config|xml|csproj|props|targets)$/.test(f))) {
  readFileSync(file, "utf8")
    .split("\n")
    .forEach((line, i) => {
      if (CREDENTIAL.test(line)) {
        problems.push(`${rel(file)}:${i + 1} possible committed credential ("Password=" / "Pwd=")`);
      }
    });
}

// --- 3. Migration triples --------------------------------------------------
if (!existsSync(MIGRATIONS_DIR)) {
  problems.push(`${rel(MIGRATIONS_DIR)} does not exist`);
} else {
  const files = readdirSync(MIGRATIONS_DIR);
  const ID_NAME = /^(\d{14}_[A-Za-z0-9_]+)$/;
  const migrationIds = new Set();
  for (const f of files) {
    const designer = f.match(/^(.+)\.Designer\.cs$/);
    const plain = f.match(/^(.+)\.cs$/);
    if (designer && ID_NAME.test(designer[1])) migrationIds.add(designer[1]);
    else if (plain && ID_NAME.test(plain[1])) migrationIds.add(plain[1]);
  }
  for (const id of migrationIds) {
    if (!files.includes(`${id}.cs`)) problems.push(`migration ${id}: missing ${id}.cs`);
    if (!files.includes(`${id}.Designer.cs`)) {
      problems.push(`migration ${id}: missing ${id}.Designer.cs`);
    } else {
      const designer = readFileSync(join(MIGRATIONS_DIR, `${id}.Designer.cs`), "utf8");
      if (!designer.includes(`[Migration("${id}")]`)) {
        problems.push(`migration ${id}: Designer does not declare [Migration("${id}")]`);
      }
    }
  }
  if (migrationIds.size === 0) problems.push("no migrations found (expected at least the T-004 Baseline)");
  if (!files.some((f) => f.endsWith("ModelSnapshot.cs"))) {
    problems.push("no *ModelSnapshot.cs found next to the migrations");
  }
}

// --- 4. dotnet-ef pinned and consistent -----------------------------------
const TOOLS = join(ROOT, ".config/dotnet-tools.json");
const MIGRATOR_CSPROJ = join(SRC, "SmartClinic.Migrator/SmartClinic.Migrator.csproj");
if (!existsSync(TOOLS)) {
  problems.push(".config/dotnet-tools.json is missing (dotnet-ef must be pinned as a local tool)");
} else {
  let efVersion;
  try {
    efVersion = JSON.parse(readFileSync(TOOLS, "utf8")).tools?.["dotnet-ef"]?.version;
  } catch (e) {
    problems.push(`.config/dotnet-tools.json is not valid JSON: ${e.message}`);
  }
  if (efVersion !== undefined) {
    if (!/^\d+\.\d+\.\d+$/.test(efVersion)) {
      problems.push(`dotnet-ef version "${efVersion}" must be an exact x.y.z (no ranges/wildcards)`);
    }
    if (existsSync(MIGRATOR_CSPROJ)) {
      const m = readFileSync(MIGRATOR_CSPROJ, "utf8").match(
        /Include="Microsoft\.EntityFrameworkCore\.Design"\s+Version="([^"]+)"/
      );
      if (!m) problems.push("Migrator csproj does not pin Microsoft.EntityFrameworkCore.Design");
      else if (m[1] !== efVersion) {
        problems.push(
          `dotnet-ef ${efVersion} != Microsoft.EntityFrameworkCore.Design ${m[1]} in the Migrator csproj`
        );
      }
    } else {
      problems.push(`${rel(MIGRATOR_CSPROJ)} is missing`);
    }
  } else if (existsSync(TOOLS)) {
    problems.push('dotnet-ef is not declared in .config/dotnet-tools.json "tools"');
  }
}

if (problems.length > 0) {
  console.error("Migration mechanism guard(s) failed:\n");
  for (const p of problems) console.error(`  ${p}`);
  console.error(`\n${problems.length} problem(s). Failing.`);
  process.exit(1);
}
console.log("Migration guards passed: controlled apply path, no committed credentials, complete migration set, pinned tooling.");
process.exit(0);
