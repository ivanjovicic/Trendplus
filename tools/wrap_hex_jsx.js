#!/usr/bin/env node
// Script: wrap_hex_jsx.js
// Purpose: Find hard-coded hex color literals in the frontend source and
// replace them with CSS variable fallbacks of the form
//    var(--c-<hex>, #<hex>)
// This is conservative: it skips token files and node_modules.

const fs = require('fs');
const path = require('path');
const glob = require('glob');

const repoRoot = path.resolve(__dirname, '..');
const srcRoot = path.join(repoRoot, 'Klijent', 'clientapp', 'src');

const ignore = [
  '**/node_modules/**',
  '**/dist/**',
  '**/build/**',
  '**/ThemeContext.tsx',
  '**/tailwind.css',
  '**/styles/themeTokens.ts',
  '**/*.min.*',
  '**/assets/**'
];

const patterns = ['**/*.tsx', '**/*.ts', '**/*.jsx', '**/*.js', '**/*.css', '**/*.scss', '**/*.svg'];

// Regex: match #RGB or #RRGGBB (3 to 6 hex digits). Skip when already inside var(...).
const hexRegex = /(?<!var\()#([0-9a-fA-F]{3,6})\b/g;

function processFile(filePath) {
  let content = fs.readFileSync(filePath, 'utf8');
  let changed = false;

  const newContent = content.replace(hexRegex, (match, hex) => {
    // keep the original case in the fallback, but use lower-case in var name
    const varName = `--c-${hex.toLowerCase()}`;
    changed = true;
    return `var(${varName}, #${hex})`;
  });

  if (changed) {
    fs.writeFileSync(filePath, newContent, 'utf8');
    console.log('Patched:', path.relative(repoRoot, filePath));
    return 1;
  }
  return 0;
}

let total = 0;
patterns.forEach((p) => {
  const files = glob.sync(path.join(srcRoot, p), { ignore });
  files.forEach((f) => {
    try {
      total += processFile(f);
    } catch (err) {
      console.error('Error processing', f, err.message);
    }
  });
});

console.log('\nDone. Files modified:', total);
