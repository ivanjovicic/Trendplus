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

function findVarRanges(text) {
  const ranges = [];
  let idx = 0;
  while (true) {
    const v = text.indexOf('var(', idx);
    if (v === -1) break;
    // find matching closing parenthesis
    let i = v + 4;
    let depth = 1;
    while (i < text.length && depth > 0) {
      const ch = text[i];
      if (ch === '(') depth++;
      else if (ch === ')') depth--;
      i++;
    }
    ranges.push([v, i]);
    idx = i;
  }
  return ranges;
}

function insideRanges(pos, ranges) {
  for (const [s, e] of ranges) if (pos >= s && pos < e) return true;
  return false;
}

function replaceInFile(file) {
  let txt = fs.readFileSync(file, 'utf8');
  const orig = txt;
  const varRanges = findVarRanges(txt);

  // 1) replace var(..., #hex) -> var(..., var(--theme-color-hex, #hex))
  txt = txt.replace(/var\(--([^,\)]+),\s*#([0-9A-Fa-f]{3,6})\)/g, (m, name, hex, offset) => {
    return `var(--${name}, var(--theme-color-${hex.toLowerCase()}, #${hex}))`;
  });

  // 2) replace standalone hex literals (not inside var(...)) -> var(--theme-color-hex, #hex)
  txt = txt.replace(/#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\b/g, (m, hex, offset) => {
    // skip if inside a var(...) range (we already handled many above)
    if (insideRanges(offset, varRanges)) return m;
    return `var(--theme-color-${hex.toLowerCase()}, ${m})`;
  });

  // 3) replace rgba(...) -> var(--theme-color-rgba-..., rgba(...)) unless inside var(...)
  txt = txt.replace(/rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})(?:\s*,\s*([\d\.]+))?\s*\)/g,
    (m, r, g, b, a, offset) => {
      if (insideRanges(offset, varRanges)) return m;
      const alphaNorm = a ? a.replace('.', 'p') : '';
      const name = `theme-color-rgba-${r}-${g}-${b}${alphaNorm ? '-' + alphaNorm : ''}`;
      return `var(--${name}, ${m})`;
    });

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
    if (replaceInFile(f)) { changed++; changedFiles.push(f.replace(path.join(__dirname, '..') + path.sep, '')); }
  } catch (e) {
    console.error('Error processing', f, e.message);
  }
}
console.log(`Processed ${files.length} files, changed ${changed} files`);
if (changed > 0) console.log('Changed files:\n' + changedFiles.join('\n'));

// exit with non-zero if nothing changed? no, exit 0
process.exit(0);
