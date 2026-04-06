const fs = require('fs');
const path = require('path');

const SRC = path.join(__dirname, '..', 'src');
const exts = new Set(['.css', '.scss', '.ts', '.tsx', '.js', '.jsx', '.html']);

function walk(dir) {
  const files = [];
  for (const name of fs.readdirSync(dir)) {
    const p = path.join(dir, name);
    const stat = fs.statSync(p);
    if (stat.isDirectory()) files.push(...walk(p));
    else if (stat.isFile() && exts.has(path.extname(name))) files.push(p);
  }
  return files;
}

function cleanFile(file) {
  let txt = fs.readFileSync(file, 'utf8');
  const orig = txt;

  // collapse double theme-color wrappers: var(--theme-color-xxx, var(--theme-color-xxx, #xxx)) -> var(--theme-color-xxx, #xxx)
  txt = txt.replace(/var\(--(theme-color-[0-9a-f]{3,6}),\s*var\(--\1,\s*#([0-9a-f]{3,6})\)\)/gi,
    (m, name, hex) => `var(--${name}, #${hex})`);

  // collapse nested wrappers where outer var uses a semantic var: var(--surface-elevated, var(--theme-color-ffffff, var(--theme-color-ffffff, #ffffff))) -> var(--surface-elevated, var(--theme-color-ffffff, #ffffff))
  txt = txt.replace(/var\((--[\w-]+),\s*var\(--(theme-color-[0-9a-f]{3,6}),\s*var\(--\2,\s*#([0-9a-f]{3,6})\)\)\)/gi,
    (m, outer, inner, hex) => `var(${outer}, var(--${inner}, #${hex}))`);

  // collapse variants where var(--c-hex, var(--theme-color-hex, var(--theme-color-hex, #hex)))
  txt = txt.replace(/var\(--c-([0-9a-f]{3,6}),\s*var\(--theme-color-\1,\s*var\(--theme-color-\1,\s*#([0-9a-f]{3,6})\)\)\)/gi,
    (m, shorthex, hex) => `var(--c-${shorthex}, var(--theme-color-${hex}, #${hex}))`);

  // remove accidental duplicate identical nested theme-color wrappers anywhere
  txt = txt.replace(/var\(--(theme-color-[0-9a-f]{3,6}),\s*var\(--\1,\s*var\(--\1,\s*#([0-9a-f]{3,6})\)\)\)/gi,
    (m, name, hex) => `var(--${name}, #${hex})`);

  // remove repeated var(--theme-color-xxx, var(--theme-color-xxx, #xxx)) with single
  txt = txt.replace(/var\(--(theme-color-[0-9a-f]{3,6}),\s*var\(--\1,\s*#([0-9a-f]{3,6})\)\)/gi,
    (m, name, hex) => `var(--${name}, #${hex})`);

  if (txt !== orig) {
    fs.writeFileSync(file, txt, 'utf8');
    return true;
  }
  return false;
}

const files = walk(SRC);
let changed = 0;
const changedFiles = [];
for (const f of files) {
  try {
    if (cleanFile(f)) { changed++; changedFiles.push(f.replace(path.join(__dirname, '..') + path.sep, '')); }
  } catch (e) {
    console.error('Error processing', f, e.message);
  }
}
console.log(`Processed ${files.length} files, cleaned ${changed} files`);
if (changed > 0) console.log('Cleaned files:\n' + changedFiles.join('\n'));
process.exit(0);
