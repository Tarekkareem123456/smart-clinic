#!/usr/bin/env node
/**
 * T-008 — Automated "no hard-coded left/right" rule (Backlog TR-14).
 *
 * Rule enforced (per Architecture / UI-UX §9 baseline): "Use logical
 * properties everywhere (margin-inline-start/end, padding-inline-*,
 * inset-inline-*, text-align: start/end, border-inline-*). No hard-coded
 * left/right."
 *
 * This is a plain Node script (no ESLint / stylelint / Tailwind plugin —
 * none of those are installed, and this repo cannot reach the npm
 * registry to install one right now). It scans source files itself and
 * exits non-zero on any violation, which is intended to fail CI/build the
 * same way scripts/check-boundaries.mjs already does for T-001. If/when a
 * real linter becomes available, this rule's checks can be ported into it
 * without changing what is or isn't allowed.
 *
 * Two independent patterns are checked, matching the two ways a hard-coded
 * direction can show up in this codebase today:
 *
 *  1. Tailwind-style utility tokens the Backlog names explicitly:
 *     ml- / mr- / pl- / pr- / left- / right-   (e.g. class="ml-4 left-0")
 *
 *  2. Plain CSS physical-direction declarations, both as a property name
 *     (`margin-left:`, `border-right:`, bare `left:`) and as a value
 *     (`float: left;`, `text-align: right;`).
 *
 * Logical-property equivalents (margin-inline-start, inset-inline-end,
 * text-align: start, etc.) are NOT flagged — that's the point.
 */

import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = fileURLToPath(new URL("..", import.meta.url));

const SCAN_DIRS = [
  join(ROOT, "apps/staff/src"),
  join(ROOT, "apps/portal/src"),
  join(ROOT, "packages/shared/src"),
];

const SCAN_EXTENSIONS = /\.(ts|tsx|js|jsx|css|scss)$/;

// (1) Tailwind-style utility tokens named explicitly in the Backlog AC.
//     Requires a non-identifier character before the token (so it doesn't
//     match inside words like "copyright-notice") and a value character
//     right after the dash (so it doesn't match a bare trailing "-").
const UTILITY_TOKEN_RE = /(?:^|[^a-zA-Z0-9_-])(ml|mr|pl|pr|left|right)-[a-zA-Z0-9]/g;

// (2a) Physical-direction CSS property names ending in left/right,
//      including the bare "left:"/"right:" properties themselves.
const CSS_PROPERTY_RE = /(?:^|[;{\s-])(?:margin|padding|border|inset|left|right)?-?(left|right)\s*:/g;

// (2b) Physical-direction CSS values: float/text-align/clear: left|right.
const CSS_VALUE_RE = /:\s*(left|right)\s*(?:!important)?\s*(?:;|}|$)/gm;

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
    if (st.isDirectory()) {
      results = results.concat(listFilesRecursive(full));
    } else if (SCAN_EXTENSIONS.test(entry)) {
      results.push(full);
    }
  }
  return results;
}

function lineNumberAt(content, index) {
  return content.slice(0, index).split("\n").length;
}

let violations = [];

for (const dir of SCAN_DIRS) {
  for (const file of listFilesRecursive(dir)) {
    const content = readFileSync(file, "utf8");
    const relPath = relative(ROOT, file);

    for (const re of [UTILITY_TOKEN_RE, CSS_PROPERTY_RE, CSS_VALUE_RE]) {
      re.lastIndex = 0;
      let match;
      while ((match = re.exec(content)) !== null) {
        violations.push({
          file: relPath,
          line: lineNumberAt(content, match.index),
          matched: match[0].trim(),
        });
      }
    }
  }
}

if (violations.length > 0) {
  console.error("Hard-coded left/right usage detected (Backlog TR-14):\n");
  for (const v of violations) {
    console.error(`  ${v.file}:${v.line}  "${v.matched}"`);
  }
  console.error(
    `\n${violations.length} violation(s). Use logical properties ` +
      "(margin-inline-start/end, inset-inline-*, text-align: start/end, " +
      "border-inline-*, etc.) instead. Failing."
  );
  process.exit(1);
}

console.log("RTL/LTR check passed: no hard-coded left/right usage found.");
process.exit(0);
