#!/usr/bin/env node
/**
 * T-001 — Automated frontend boundary enforcement.
 *
 * Rule enforced (per Smart Clinic Architecture — Portal isolation):
 *   apps/portal  MUST NOT import anything from apps/staff.
 *   packages/shared MUST NOT import from any app (apps/*), since it is
 *     meant to be consumed by apps, not depend on them.
 *
 * This is a plain Node script (no third-party dependency) so it can run
 * in CI or locally with `node scripts/check-boundaries.mjs` and without
 * requiring package installation. It exits with a non-zero status code
 * on any violation, which is intended to fail the build/CI step.
 *
 * Scope note: this script only checks *source* import statements
 * (static `import ... from '...'` / `require('...')`) inside src/ of
 * each workspace. It intentionally does not scan node_modules.
 */

import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = fileURLToPath(new URL("..", import.meta.url));

const RULES = [
  {
    label: "apps/portal -> apps/staff",
    sourceDir: join(ROOT, "apps/portal/src"),
    forbiddenPathFragments: ["apps/staff", "../staff", "staff/src"],
    forbiddenPackageNames: ["@smart-clinic/staff"],
  },
  {
    label: "packages/shared -> apps/*",
    sourceDir: join(ROOT, "packages/shared/src"),
    forbiddenPathFragments: ["apps/staff", "apps/portal", "../../apps"],
    forbiddenPackageNames: ["@smart-clinic/staff", "@smart-clinic/portal"],
  },
];

const IMPORT_RE = /(?:import[^'"]*from\s*|require\()\s*['"]([^'"]+)['"]/g;

function listFilesRecursive(dir) {
  let results = [];
  let entries;
  try {
    entries = readdirSync(dir);
  } catch {
    return results; // dir may not exist yet — nothing to check
  }
  for (const entry of entries) {
    const full = join(dir, entry);
    const st = statSync(full);
    if (st.isDirectory()) {
      results = results.concat(listFilesRecursive(full));
    } else if (/\.(ts|tsx|js|jsx|mjs|cjs)$/.test(entry)) {
      results.push(full);
    }
  }
  return results;
}

let violations = [];

for (const rule of RULES) {
  const files = listFilesRecursive(rule.sourceDir);
  for (const file of files) {
    const content = readFileSync(file, "utf8");
    let match;
    while ((match = IMPORT_RE.exec(content)) !== null) {
      const importPath = match[1];
      const hitsFragment = rule.forbiddenPathFragments.some((f) =>
        importPath.includes(f)
      );
      const hitsPackage = rule.forbiddenPackageNames.some(
        (p) => importPath === p || importPath.startsWith(p + "/")
      );
      if (hitsFragment || hitsPackage) {
        violations.push({
          rule: rule.label,
          file: relative(ROOT, file),
          importPath,
        });
      }
    }
  }
}

if (violations.length > 0) {
  console.error("Architectural boundary violation(s) detected:\n");
  for (const v of violations) {
    console.error(`  [${v.rule}] ${v.file} imports "${v.importPath}"`);
  }
  console.error(`\n${violations.length} violation(s). Failing.`);
  process.exit(1);
}

console.log("Boundary check passed: no forbidden cross-app imports found.");
process.exit(0);
