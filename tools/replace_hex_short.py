import os
import re

ROOT = os.path.join(os.getcwd(), 'Klijent', 'clientapp', 'src')
EXCLUDE = {
    os.path.normpath(os.path.join(ROOT, 'context', 'ThemeContext.tsx')),
    os.path.normpath(os.path.join(ROOT, 'skeleton.css')),
}
# Match 3,4 or 6 hex digits (#fff, #abcd, #aabbcc)
hex_re = re.compile(r"#[0-9a-fA-F]{3,4}\b|#[0-9a-fA-F]{6}\b")

changed = []
for dirpath, dirnames, filenames in os.walk(ROOT):
    for fn in filenames:
        path = os.path.normpath(os.path.join(dirpath, fn))
        if path in EXCLUDE:
            continue
        if not path.endswith(('.css', '.scss', '.tsx', '.ts', '.svg', '.jsx', '.js')):
            continue
        try:
            with open(path, 'r', encoding='utf-8') as f:
                text = f.read()
        except Exception:
            continue
        new_text_parts = []
        last = 0
        modified = False
        for m in hex_re.finditer(text):
            start, end = m.start(), m.end()
            # Determine line start and content
            line_start = text.rfind('\n', 0, start) + 1
            line_end_idx = text.find('\n', start)
            if line_end_idx == -1:
                line_end_idx = len(text)
            line = text[line_start:line_end_idx]
            # Skip if 'var(' appears before the match on same line
            idx_in_line = start - line_start
            if 'var(' in line[:idx_in_line]:
                continue
            hexval = m.group(0)
            key = hexval.lstrip('#').lower()
            replacement = f"var(--c-{key}, {hexval})"
            new_text_parts.append(text[last:start])
            new_text_parts.append(replacement)
            last = end
            modified = True
        if not modified:
            continue
        new_text_parts.append(text[last:])
        out = ''.join(new_text_parts)
        with open(path, 'w', encoding='utf-8') as f:
            f.write(out)
        changed.append(path)

print('Changed files:', len(changed))
for p in changed:
    print(p)
