let puppeteer;
try {
  puppeteer = require('puppeteer');
} catch (e) {
  try {
    puppeteer = require('puppeteer-core');
  } catch (e2) {
    // try to resolve from Klijent/clientapp node_modules
    try {
      const path = require('path');
      puppeteer = require(path.resolve(__dirname, '..', 'Klijent', 'clientapp', 'node_modules', 'puppeteer'));
    } catch (e3) {
      try {
        const path = require('path');
        puppeteer = require(path.resolve(__dirname, '..', 'Klijent', 'clientapp', 'node_modules', 'puppeteer-core'));
      } catch (e4) {
        console.error('puppeteer not found. Install with: npm install puppeteer (in Klijent/clientapp or repo root)');
        process.exit(1);
      }
    }
  }
}

const BASE = 'http://127.0.0.1:5174';
const ROUTES = [
  '/',
  '/analytics',
  '/analytics/supplier-sales-stats',
  '/analytics/shoe-type-sales-stats',
  '/analytics/color-sales-stats',
  '/trend-dashboard',
  '/amazon-shoes',
  '/ebay-shoes',
  '/google-shopping'
];

// Light theme CSS variables (conservative subset from ThemeContext)
const LIGHT_VARS = {
  '--surface-default': '#f4f7fb',
  '--surface-light': '#ffffff',
  '--surface-elevated': '#ffffff',
  '--surface-darker': '#e6edf7',
  '--text-primary': '#0f172a',
  '--text-secondary': '#334155',
  '--text-muted': '#64748b',
  '--border-default': '#d3dce9',
  '--border-hover': '#a7b9d3',
  '--focus-ring': '#2563eb',
  '--success': '#10b981',
  '--error': '#ef4444',
  '--warning': '#f59e0b',
  '--info': '#3b82f6',
  '--text-on-primary': '#ffffff'
};

function parseRgb(str) {
  if (!str) return null;
  str = str.trim();
  if (str.startsWith('#')) {
    const hex = str.slice(1);
    const bigint = parseInt(hex.length===3 ? hex.split('').map(s=>s+s).join('') : hex, 16);
    return [(bigint>>16)&255, (bigint>>8)&255, bigint&255, 1];
  }
  const m = str.match(/rgba?\(([^)]+)\)/);
  if (!m) return null;
  const parts = m[1].split(',').map(s=>s.trim());
  const r = parseInt(parts[0],10), g = parseInt(parts[1],10), b = parseInt(parts[2],10);
  const a = parts[3] ? parseFloat(parts[3]) : 1;
  return [r,g,b,a];
}

function luminance([r,g,b]){
  const rs = r/255, gs = g/255, bs = b/255;
  const toLin = c => c <= 0.03928 ? c/12.92 : Math.pow((c+0.055)/1.055, 2.4);
  return 0.2126*toLin(rs) + 0.7152*toLin(gs) + 0.0722*toLin(bs);
}

function contrastRatio(rgb1, rgb2){
  const L1 = luminance(rgb1); const L2 = luminance(rgb2);
  const lighter = Math.max(L1,L2), darker = Math.min(L1,L2);
  return (lighter+0.05)/(darker+0.05);
}

(async ()=>{
  const exePath = process.env.PUPPETEER_EXECUTABLE_PATH || 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
  const launchOpts = {args:['--no-sandbox','--disable-setuid-sandbox'], headless: true};
  launchOpts.executablePath = launchOpts.executablePath || exePath;
  const browser = await puppeteer.launch(launchOpts);
  const page = await browser.newPage();
  page.setDefaultNavigationTimeout(30000);

  const results = [];
  for (const route of ROUTES) {
    const url = BASE + route;
    try {
      const res = await page.goto(url, {waitUntil: 'networkidle0'});
      if (!res || res.status() >= 400) {
        results.push({route, error: `HTTP ${res ? res.status() : 'no response'}`});
        continue;
      }
      // Inject light theme variables
      await page.evaluate((vars)=>{
        const root = document.documentElement;
        for (const k in vars) root.style.setProperty(k, vars[k]);
        // also mark data-theme=light if used by app
        root.setAttribute('data-theme', 'light');
      }, LIGHT_VARS);
      await new Promise((resolve) => setTimeout(resolve, 400));

      // Collect visible text elements
      const issues = await page.evaluate(()=>{
        function parseRgbInline(s){
          s=s||''; s=s.trim(); if (s.startsWith('#')) return s;
          const m = s.match(/rgba?\(([^)]+)\)/); return m?m[0]:null;
        }
        function getEffectiveBackground(el){
          let node = el;
          while(node && node.nodeType===1){
            const bg = getComputedStyle(node).backgroundColor;
            if (bg && bg !== 'rgba(0, 0, 0, 0)' && bg !== 'transparent') return bg;
            node = node.parentElement;
          }
          return getComputedStyle(document.body).backgroundColor || '#fff';
        }
        const candidates = Array.from(document.querySelectorAll('p,span,h1,h2,h3,h4,h5,h6,a,button,label,td,th,li'));
        const visible = candidates.filter(el=>{
          const style = getComputedStyle(el);
          if (style.visibility==='hidden' || style.display==='none') return false;
          const rect = el.getBoundingClientRect();
          if (rect.width===0 || rect.height===0) return false;
          return true;
        }).slice(0,200);
        const out = [];
        for (const el of visible){
          const cs = getComputedStyle(el);
          const color = cs.color;
          const bg = getEffectiveBackground(el);
          const fontSize = parseFloat(cs.fontSize);
          const fontWeight = cs.fontWeight;
          out.push({selector: el.tagName.toLowerCase() + (el.id?('#'+el.id):'') + (el.className?('.'+el.className.split(' ').join('.')):''), color, bg, fontSize, fontWeight, text: el.textContent?.slice(0,80)});
        }
        return out;
      });

      // Compute ratios in Node (parse RGB)
      const pageIssues = [];
      for (const item of issues){
        const c = parseRgb(item.color);
        const b = parseRgb(item.bg) || [255,255,255,1];
        if (!c || !b) continue;
        const ratio = contrastRatio(c, b);
        const large = item.fontSize >= 18 || (item.fontSize>=14 && parseInt(item.fontWeight)>=700);
        const min = large ? 3.0 : 4.5;
        if (ratio < min) pageIssues.push({...item, ratio: Math.round(ratio*100)/100, required: min});
      }

      results.push({route, issues: pageIssues.slice(0,20), totalIssues: pageIssues.length});

    } catch (e){
      results.push({route, error: String(e)});
    }
  }

  console.log(JSON.stringify(results, null, 2));
  await browser.close();
})();
