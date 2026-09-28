#!/usr/bin/env node
/**
 * T-003 — lightweight structural sanity check for the new .sql files.
 *
 * This sandbox has no PostgreSQL/psql available and no network access to
 * install one (see the T-003 report), so the SQL in backend/database/
 * could not be executed against a real database from here. This script
 * is NOT a substitute for that — it only catches a narrow class of
 * mechanical mistakes (unbalanced $$ dollar-quoting, unbalanced
 * parentheses, a file that doesn't end in a terminated statement) so that
 * at least those are caught automatically before a human/CI runs the real
 * thing against PostgreSQL.
 */

import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = fileURLToPath(new URL("..", import.meta.url));
const SQL_DIR = join(ROOT, "backend/database");

function listSqlFiles(dir) {
  let results = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    const st = statSync(full);
    if (st.isDirectory()) results = results.concat(listSqlFiles(full));
    else if (/\.sql(\.template)?$/.test(entry)) results.push(full);
  }
  return results;
}

function stripSqlComments(sql) {
  // Remove -- line comments (does not need to handle /* */ here: none used).
  return sql
    .split("\n")
    .map((line) => line.replace(/--.*$/, ""))
    .join("\n");
}

let problems = [];

for (const file of listSqlFiles(SQL_DIR)) {
  const rel = relative(ROOT, file);
  const raw = readFileSync(file, "utf8");
  const code = stripSqlComments(raw);

  const dollarQuoteCount = (code.match(/\$\$/g) || []).length;
  if (dollarQuoteCount % 2 !== 0) {
    problems.push(`${rel}: unbalanced $$ dollar-quoting (found ${dollarQuoteCount} occurrences, expected an even number)`);
  }

  const opens = (code.match(/\(/g) || []).length;
  const closes = (code.match(/\)/g) || []).length;
  if (opens !== closes) {
    problems.push(`${rel}: unbalanced parentheses (${opens} '(' vs ${closes} ')')`);
  }

  const trimmed = code.trim();
  if (trimmed.length === 0) {
    problems.push(`${rel}: file is empty`);
  } else if (!/[;]\s*$/.test(trimmed)) {
    problems.push(`${rel}: does not end with a terminated statement (missing trailing ';')`);
  }

  const beginCount = (code.match(/\bBEGIN\b/gi) || []).length;
  const endCount = (code.match(/\bEND\b/gi) || []).length;
  if (beginCount > 0 && endCount < beginCount) {
    problems.push(`${rel}: ${beginCount} BEGIN keyword(s) but only ${endCount} END keyword(s) — likely unclosed block`);
  }
}

if (problems.length > 0) {
  console.error("SQL structural sanity check found issue(s):\n");
  for (const p of problems) console.error(`  ${p}`);
  console.error(`\n${problems.length} issue(s). Failing.`);
  process.exit(1);
}

console.log(
  `SQL structural sanity check passed on ${listSqlFiles(SQL_DIR).length} file(s). ` +
    "NOTE: this only checks mechanical structure (balanced $$/parens/terminators) — " +
    "it is not a substitute for running these scripts against real PostgreSQL."
);
process.exit(0);
