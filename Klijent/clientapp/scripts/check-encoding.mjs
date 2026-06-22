#!/usr/bin/env node
/*
Encoding guardrail for maintained docs and frontend source.

Scanned roots:
 - docs/analytics
 - docs/pilot
 - docs/qa
 - docs/security
 - docs/demo
 - Klijent/clientapp/src/components
 - Klijent/clientapp/src/pages
 - Klijent/clientapp/src/services
 - Klijent/clientapp/src/utils

Allowlist:
 - legacy benchmark/scraper surfaces that are outside the current analytics pilot
 - test files, because some specs intentionally contain mojibake samples/assertions
*/

import fs from 'fs/promises';
import path from 'path';

const base = process.cwd();
const scanRoots = [
  'docs/analytics',
  'docs/pilot',
  'docs/qa',
  'docs/security',
  'docs/demo',
  'Klijent/clientapp/src/components',
  'Klijent/clientapp/src/pages',
  'Klijent/clientapp/src/services',
  'Klijent/clientapp/src/utils',
];

const ignoredExactFiles = new Set([
  'Klijent/clientapp/src/components/TrendDashboard.tsx',
  'Klijent/clientapp/src/pages/DeichmannPage.tsx',
]);

const ignoredPathParts = [
  '/__tests__/',
];

const ignoredFilePatterns = [
  /\.test\./i,
  /\.spec\./i,
];

const mojibakePatterns = [
  { name: 'replacement-char', re: /�/ },
  { name: 'classic-utf8-mojibake', re: /Ã„|Ã…|Ã¢|Ã—|Ã¡|Ã©|Ã¨|Ã±|Ã¶|Ã¼/ },
  { name: 'serbian-latin-mojibake', re: /Ä|Ä‡|Ä‘|Å¡|Å¾/ },
  { name: 'box-drawing-mojibake', re: /â€“|â€”|â€¢|â†’|â†‘|â†“|â”€|â•|â‚¬/ },
  { name: 'emoji-mojibake', re: /ðŸ/ },
];

function relPath(filePath) {
  return path.relative(base, filePath).replaceAll('\\', '/');
}

function isIgnoredFile(rel) {
  if (ignoredExactFiles.has(rel)) return true;
  if (ignoredPathParts.some((part) => rel.includes(part))) return true;
  return ignoredFilePatterns.some((pattern) => pattern.test(rel));
}

async function walk(dir) {
  const entries = await fs.readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await walk(full)));
      continue;
    }
    files.push(full);
  }
  return files;
}

async function scanFile(filePath) {
  const content = await fs.readFile(filePath, 'utf8');
  const lines = content.split(/\r?\n/);
  const findings = [];

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    const pattern = mojibakePatterns.find((candidate) => candidate.re.test(line));
    if (!pattern) continue;
    findings.push({
      lineNumber: index + 1,
      pattern: pattern.name,
      line: line.trim(),
    });
  }

  return findings;
}

async function main() {
  const findings = [];

  for (const root of scanRoots) {
    const absoluteRoot = path.join(base, root);
    try {
      const files = await walk(absoluteRoot);
      for (const filePath of files) {
        if (!/\.(md|mdx|txt|ts|tsx|js|jsx)$/i.test(filePath)) continue;
        const rel = relPath(filePath);
        if (isIgnoredFile(rel)) continue;

        const fileFindings = await scanFile(filePath);
        for (const finding of fileFindings) {
          findings.push({ rel, ...finding });
        }
      }
    } catch {
      // Missing roots are fine in smaller branches.
    }
  }

  if (findings.length > 0) {
    for (const finding of findings) {
      console.error(`MOJIBAKE: ${finding.rel}:${finding.lineNumber} [${finding.pattern}] ${finding.line}`);
    }
    console.error(`\nFound ${findings.length} mojibake issue(s).`);
    process.exit(2);
  }

  console.log('OK: No mojibake detected in maintained docs/frontend surfaces.');
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack ?? error.message : String(error));
  process.exit(1);
});
