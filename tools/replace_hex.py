import os
import re

ROOT = os.path.join(os.getcwd(), 'Klijent', 'clientapp', 'src')
EXCLUDE = {
    os.path.normpath(os.path.join(ROOT, 'context', 'ThemeContext.tsx')),
    os.path.normpath(os.path.join(ROOT, 'skeleton.css')),
}
hex_re = re.compile(r"#[0-9a-fA-F]{6}")

changed = []
for dirpath, dirnames, filenames in os.walk(ROOT):
    for fn in filenames:
        path = os.path.normpath(os.path.join(dirpath, fn))
        if path in EXCLUDE:
            continue
        if not path.endswith(('.css', '.scss', '.tsx', '.ts', '.svg', '.jsx', '.js')):
            continue
        with open(path, 'r', encoding='utf-8') as f:
            text = f.read()
        new_text = []
        last = 0
        modified = False
        for m in hex_re.finditer(text):
            start, end = m.start(), m.end()
            # Determine line start
            line_start = text.rfind('\n', 0, start) + 1
            line = text[line_start:text.find('\n', start) if text.find('\n', start)!=-1 else len(text)]
            # If 'var(' appears before the match on same line, skip replacement
            idx_in_line = start - line_start
            if 'var(' in line[:idx_in_line]:
                continue
            hexval = m.group(0)
            key = hexval.lstrip('#').lower()
            replacement = f"var(--c-{key}, {hexval})"
            new_text.append(text[last:start])
            new_text.append(replacement)
            last = end
            modified = True
        if not modified:
            continue
        new_text.append(text[last:])
        out = ''.join(new_text)
        with open(path, 'w', encoding='utf-8') as f:
            f.write(out)
        changed.append(path)

print('Changed files:', len(changed))
for p in changed:
    print(p)
