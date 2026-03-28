#!/usr/bin/env node
const fs = require('fs');
const path = require('path');
const glob = require('glob');

const repoRoot = path.resolve(__dirname, '..');
const srcRoot = path.join(repoRoot, 'Klijent', 'clientapp', 'src');

const ignore = ['**/node_modules/**', '**/dist/**', '**/build/**', '**/styles/themeTokens.ts', '**/context/ThemeContext.tsx'];

// Mapping of token -> fallback hex
const FALLBACKS = {
  '--border': '#d1d5db',
  '--border-default': '#d3dce9',
  '--primary': '#2563eb',
  '--success': '#10b981',
  '--error': '#ef4444',
  '--warning': '#f59e0b',
  '--info': '#3b82f6',
  '--text-primary': '#0f172a',
  '--text-muted': '#64748b',
  '--surface': '#ffffff',
  '--surface-default': '#f4f7fb',
  '--surface-elevated': '#ffffff',
  '--focus-ring': '#2563eb',
  '--muted': '#9ca3af',
  '--accent-primary': '#2563eb',
  '--icon-stroke': '#374151'
};

const patterns = ['**/*.tsx','**/*.ts','**/*.jsx','**/*.js','**/*.css','**/*.svg'];

const varNoFallbackRegex = /var\(\s*(--[a-zA-Z0-9-_]+)\s*\)/g;

function processFile(filePath){
  let content = fs.readFileSync(filePath,'utf8');
  let changed = false;
  content = content.replace(varNoFallbackRegex, (m, token)=>{
    if(!(token in FALLBACKS)) return m;
    changed = true;
    return `var(${token}, ${FALLBACKS[token]})`;
  });
  if(changed){
    fs.writeFileSync(filePath, content, 'utf8');
    console.log('Patched:', path.relative(repoRoot, filePath));
    return 1;
  }
  return 0;
}

let total = 0;
patterns.forEach(p=>{
  const files = glob.sync(path.join(srcRoot, p), { ignore });
  files.forEach(f=>{
    try{ total += processFile(f); } catch(e){ console.error('err', f, e.message); }
  });
});

console.log('\nDone. Files modified:', total);
