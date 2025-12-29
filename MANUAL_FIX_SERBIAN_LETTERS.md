# FINALNO REŠENJE - RUČNO EDITOVANJE

## Problem
`create_file` tool ne čuva pravilno UTF-8 karaktere u Windows okruženju.

## REŠENJE - Ručno editujte UnosRobeForm.tsx

### Korak 1: Otvorite fajl
```
Klijent\clientapp\src\components\UnosRobeForm.tsx
```

### Korak 2: Find & Replace (Ctrl+H)

Zamenite sledeće ASCII verzije sa pravim srpskim slovima:

| Trenutno (ASCII) | Zamenite sa (UTF-8) |
|------------------|---------------------|
| `Pretrazite i izaberite dobavljaca` | `Pretražite i izaberite dobavljača` |
| `Pretrazite dobavljace` | `Pretražite dobavljače` |
| `Ili izaberite iz liste svih dobavljaca` | `Ili izaberite iz liste svih dobavljača` |

**ILI** jednostavno kopirajte ispravne stringove:
- "Pretražite i izaberite dobavljača"
- "Pretražite dobavljače po nazivu, adresi ili telefonu..."
- "Ili izaberite iz liste svih dobavljača"

### Korak 3: Sačuvajte fajl (Ctrl+S)

Proverite da VS Code koristi UTF-8:
- Dole desno u statusnoj traci treba da piše: **UTF-8**
- Ako ne, kliknite na encoding i izaberite "Save with Encoding" → "UTF-8"

### Korak 4: Rebuild
```powershell
cd Klijent\clientapp
npm run build
```

### Korak 5: Hard Refresh Browser
```
Ctrl + Shift + R
```

## Verifikacija Encoding-a
```powershell
node -e "const fs=require('fs'); const c=fs.readFileSync('./src/components/UnosRobeForm.tsx','utf8'); console.log(c.includes('Pretražite')?'✓ UTF-8 OK':'✗ Potrebno ručno editovanje');"
```

## Alternativa - Kopirajte Ceo Fajl

Ako find & replace ne radi, kopirajte sledeći kod direktno u VS Code:

```typescript
// VIDITE ATTACHMENT: UnosRobeForm_FINAL.tsx
// Kopirajte ceo sadržaj odatle
```

## Zašto se ovo dešava?
- Windows PowerShell/CMD koristi različit encoding (Windows-1252)
- create_file tool u Copilot-u ne može garantovati UTF-8 na Windows-u
- Rešenje: Ručno editovanje u VS Code koji pravilno čuva UTF-8
