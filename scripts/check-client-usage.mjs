#!/usr/bin/env node
/**
 * T-005 (Backlog TR-11 / Technical Plan §9.5) — "Services are the only
 * place that knows the shape of the endpoints and returns typed models."
 *
 * Rule enforced: nothing under apps/*&#47;src, EXCEPT files under a
 * services/ directory, may import from a generated/ directory (the
 * per-app OpenAPI-generated client — see apps/*&#47;src/generated).
 *
 * Zero-dependency Node script, same pattern as scripts/check-boundaries.mjs
 * and scripts/check-rtl-ltr.mjs.
 */

import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = fileURLToPath(new URL("..", import.meta.url));
const APPS_DIR = join(ROOT, "apps");

const IMPORT_RE = /(?:import[^'"]*from\s*|require\()\s*['"]([^'"]+)['"]/g;

function listFilesRecursive(dir) {
  let results = [];
  let entries;
  try {
    entries = readdirSync(dir);
  } catch {
    return results;
  }
  for (const entry of entries) {
    const full = join(dir, entry);
    const st = statSync(full);
    if (st.isDirectory()) results = results.concat(listFilesRecursive(full));
    else if (/\.(ts|tsx|js|jsx|mjs|cjs)$/.test(entry)) results.push(full);
  }
  return results;
}

const isInsideServicesDir = (relPath) => relPath.split(sep).includes("services");
const isInsideGeneratedDir = (relPath) => relPath.split(sep).includes("generated");
const importsGenerated = (importPath) => importPath.includes("/generated/") || importPath.includes("../generated");

let violations = [];

for (const file of listFilesRecursive(APPS_DIR)) {
  const relPath = relative(ROOT, file);
  if (isInsideServicesDir(relPath) || isInsideGeneratedDir(relPath)) continue; // allowed

  const content = readFileSync(file, "utf8");
  let match;
  IMPORT_RE.lastIndex = 0;
  while ((match = IMPORT_RE.exec(content)) !== null) {
    const importPath = match[1];
    if (importsGenerated(importPath)) {
      violations.push({ file: relPath, importPath });
    }
  }
}

if (violations.length > 0) {
  console.error("TR-11 violation(s) — generated client imported outside services/*:\n");
  for (const v of violations) {
    console.error(`  ${v.file} imports "${v.importPath}"`);
  }
  console.error(`\n${violations.length} violation(s). Failing.`);
  process.exit(1);
}

console.log("TR-11 check passed: generated API clients are imported only from services/*.");
process.exit(0);
