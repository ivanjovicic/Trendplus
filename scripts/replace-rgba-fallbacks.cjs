#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

function walk(dir, fileList = []) {
  const files = fs.readdirSync(dir, { withFileTypes: true });
  for (const f of files) {
    const full = path.join(dir, f.name);
    if (f.isDirectory()) walk(full, fileList);
    else fileList.push(full);
  }
  return fileList;
}

function makeTokenName(r, g, b, a) {
  const alpha = String(a).replace('.', 'p');
  return `--theme-color-rgba-${r}-${g}-${b}-${alpha}`;
}

function processFile(file) {
  const ext = path.extname(file).toLowerCase();
  const allowed = ['.css', '.scss', '.tsx', '.ts', '.jsx', '.js', '.html'];
  if (!allowed.includes(ext)) return false;
  let content = fs.readFileSync(file, 'utf8');
  let changed = false;

  const rgbaRegex = /rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d*\.?\d+)\s*\)/g;

  content = content.replace(rgbaRegex, (match, r, g, b, a, offset) => {
    // skip if already wrapped in var(--theme-color-rgba-...)
    const before = content.slice(Math.max(0, offset - 40), offset);
    if (before.includes('var(') && before.match(/--theme-color-rgba-/)) return match;

    const token = makeTokenName(r, g, b, a);
    changed = true;
    return `var(${token}, ${match})`;
  });

  if (changed) fs.writeFileSync(file, content, 'utf8');
  return changed;
}

function main() {
  const base = process.argv[2] || 'Klijent/clientapp/src/pages';
  const absBase = path.resolve(process.cwd(), base);
  if (!fs.existsSync(absBase)) {
    console.error('Base path does not exist:', absBase);
    process.exit(2);
  }

  const files = walk(absBase);
  let changedFiles = [];
  for (const f of files) {
    try {
      if (processFile(f)) changedFiles.push(f);
    } catch (err) {
      console.error('Error processing', f, err.message);
    }
  }

  console.log(`Processed ${files.length} files, changed ${changedFiles.length} files`);
  if (changedFiles.length) console.log(changedFiles.join('\n'));
}

main();
