#!/usr/bin/env node
/*
Allowlist (files/paths where analytics rules are allowed):
 - src/utils/analyticsFormatters.ts
 - src/utils/analyticsConstants.ts
 - src/utils/analyticsMetricDescriptions.ts
 - any test files (*.test.* or __tests__ folders)
 - explicit utils under components (developer review OK)

This script scans clientapp source files for forbidden local analytics/business-logic patterns.
It prints filename:line and offending line and exits with code 2 when violations found.
*/

import fs from 'fs/promises';
import path from 'path';

const base = process.cwd(); // expected to run from Klijent/clientapp
const targets = ['src/pages', 'src/components', 'src/services', 'src/utils'];
const allowedRelative = [
  'src/utils/analyticsFormatters.ts',
  'src/utils/analyticsConstants.ts',
  'src/utils/analyticsMetricDescriptions.ts'
];

const rules = [
  { name: 'BOOST_SCORE_THRESHOLD', re: /\bconst\s+BOOST_SCORE_THRESHOLD\b/, scopes: ['pages','components'] },
  { name: 'KEEP_SCORE_THRESHOLD', re: /\bconst\s+KEEP_SCORE_THRESHOLD\b/, scopes: ['pages','components'] },

  { name: 'fmtRsd', re: /\bfunction\s+fmtRsd\b|\bconst\s+fmtRsd\b/, scopes: ['pages','components'] },
  { name: 'fmtPct', re: /\bfunction\s+fmtPct\b|\bconst\s+fmtPct\b/, scopes: ['pages','components'] },
  { name: 'formatCurrency', re: /\bfunction\s+formatCurrency\b|\bconst\s+formatCurrency\b|\bexport\s+function\s+formatCurrency\b/, scopes: ['pages'] },
  { name: 'formatPercent', re: /\bfunction\s+formatPercent\b|\bconst\s+formatPercent\b|\bexport\s+function\s+formatPercent\b/, scopes: ['pages'] },

  { name: 'decisionScore_assign', re: /\bdecisionScore\b\s*=/, scopes: ['pages','components'] },
  { name: 'qualityIndex_mul', re: /qualityIndex\s*\*/, scopes: ['pages','components'] },
  { name: 'score_mul_zero', re: /score\s*\*\s*0\./, scopes: ['pages','components','services','utils'] },
  { name: 'marginCoverage_mul_zero', re: /marginCoveragePct\s*\*\s*0\./, scopes: ['pages','components'] },
  { name: 'trendNorm', re: /\btrendNorm\b/, scopes: ['pages','components'] },
  { name: 'shareNorm', re: /\bshareNorm\b/, scopes: ['pages','components'] },

  { name: 'confidencePct_assign', re: /\bconfidencePct\b\s*=/, scopes: ['pages','components'] },
  { name: 'reliabilityPct_assign', re: /\breliabilityPct\b\s*=/, scopes: ['pages','components'] },
  { name: 'recommendationStatus_assign', re: /\brecommendationStatus\b\s*=/, scopes: ['pages','components'] }
];

function isTestFile(rel) {
  return /\.test\.|\.spec\.|\/__tests__\//.test(rel);
}

async function walk(dir) {
  let entries = await fs.readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await walk(full)));
    } else {
      files.push(full);
    }
  }
  return files;
}

function relPath(p) {
  return path.relative(base, p).replaceAll('\\\\', '/');
}

(async () => {
  let violations = 0;

  for (const t of targets) {
    const dir = path.join(base, t);
    try {
      const allFiles = await walk(dir);
      for (const file of allFiles) {
        if (!/\.(ts|tsx|js|jsx)$/.test(file)) continue;
        const rel = relPath(file);
        if (isTestFile(rel)) continue;

        // allow explicit utils
        if (allowedRelative.some(a => rel === a)) continue;

        const content = await fs.readFile(file, 'utf8');
        const category = rel.startsWith('src/pages/') ? 'pages'
          : rel.startsWith('src/components/') ? 'components'
          : rel.startsWith('src/services/') ? 'services'
          : rel.startsWith('src/utils/') ? 'utils'
          : 'other';

        for (const rule of rules) {
          if (!rule.scopes.includes(category)) continue;
          const m = content.match(rule.re);
          if (m) {
            // find line number
            const lines = content.split(/\r?\n/);
            let lineno = 1;
            for (let i = 0; i < lines.length; i++) {
              if (rule.re.test(lines[i])) { lineno = i + 1; break; }
            }
            console.error(`VIOLATION: ${rel}:${lineno} -> ${rule.name} -- ${lines[lineno-1].trim()}`);
            violations++;
          }
        }
      }
    } catch (err) {
      // ignore missing directories
    }
  }

  if (violations > 0) {
    console.error(`\nFound ${violations} guardrail violation(s). See list above.`);
    process.exit(2);
  }

  console.log('OK: No guardrail violations detected.');
  process.exit(0);
})();
