HEX Replacement Script
======================

What
----
This repository includes a small script `tools/wrap_hex_jsx.js` that
searches the frontend source (`Klijent/clientapp/src`) for hard-coded
hex color literals (e.g. `#fff`, `#00aabb`) and replaces them with
CSS variable fallbacks of the form:

  var(--c-00aabb, #00aabb)

Why
---
Using `var(--c-..., #hex)` ensures the page has a safe pre-hydration
fallback while allowing runtime theme tokens to override the color.

Important
---------
- The script modifies files in-place. Commit or stash changes first.
- It intentionally ignores token files such as `ThemeContext.tsx`,
  `tailwind.css` and `styles/themeTokens.ts` so it won't overwrite
  authoritative token definitions.

Requirements
------------
- Node.js (12+)
- The `glob` npm package (install via `npm i glob` or `npm i -D glob`)

How to run
----------
From the repo root:

```bash
cd c:\Users\Ivan\source\repos\Trendplus2
npm install glob --no-audit --no-fund
node tools/wrap_hex_jsx.js
```

The script will print each patched file and a final summary.

What it changes
---------------
- Replaces hex literals like `#fff` or `#112233` with
  `var(--c-fff, #fff)` / `var(--c-112233, #112233)` across `.tsx,.ts,.jsx,.js,.css,.scss,.svg`.
- Skips files in `node_modules`, `dist`, and some token files.

Notes & Cautions
----------------
- The regex is conservative but not perfect. Review diffs after running.
- It uses a negative lookbehind to avoid replacing hexes already inside
  `var(...)`. If your Node version does not support the regex, run
  the script with a newer Node.js.
