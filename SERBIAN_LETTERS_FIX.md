# UPUTSTVO ZA REŠAVANJE PROBLEMA SA SRPSKIM SLOVIMA

## Problem
Vidite `?` znake umesto srpskih slova (?, ?, š, ž, ?) na stranici.

## Uzrok
Browser cache - stari build fajlovi su keširani u memoriji browsera.

## Rešenje

### Opcija 1: Hard Refresh (Preporu?eno)
1. Otvorite aplikaciju u browseru
2. Pritisnite **Ctrl + Shift + R** (ili **Ctrl + F5**)
3. Browser ?e u?itati najnovije fajlove

### Opcija 2: Incognito/Private Mode
1. Otvorite aplikaciju u **Incognito modu**:
   - Chrome: **Ctrl + Shift + N**
   - Firefox: **Ctrl + Shift + P**
   - Edge: **Ctrl + Shift + N**
2. Srpska slova bi trebalo da se prikazuju pravilno

### Opcija 3: Clear Browser Cache
1. Otvorite Developer Tools (**F12**)
2. Desni klik na **Refresh** dugme
3. Izaberite **"Empty Cache and Hard Reload"**

### Opcija 4: Clear All Cache
**Chrome/Edge:**
1. Settings ? Privacy and security ? Clear browsing data
2. Izaberite **Cached images and files**
3. Kliknite **Clear data**

**Firefox:**
1. Settings ? Privacy & Security ? Cookies and Site Data
2. Kliknite **Clear Data**
3. Ozna?ite **Cached Web Content**

## Verifikacija
Nakon bilo koje od opcija, otvorite stranicu i proverite:
- ? "Broj ra?una" (ne "Broj ra?una")
- ? "Dobavlja?" (ne "Dobavlja?")
- ? "Pretražite" (ne "Pretra?ite")

## Tehni?ki detalji
- Build fajlovi sada koriste **esbuild minifier** koji ?uva UTF-8 karaktere
- Source fajlovi su UTF-8 encoded
- Verification: `npm run build` generiše pravilne UTF-8 fajlove

## Ako problem i dalje postoji
Kontaktirajte podršku ili pokrenite:
```powershell
cd C:/Users/Ivan/source/repos/Trendplus2/Klijent/clientapp
node -e "const fs = require('fs'); const content = fs.readFileSync('./dist/assets/index-D61RbFAL.js', 'utf8'); console.log(content.includes('Greška') ? '? Build je OK' : '? Rebuild needed');"
```

Ako kaže "? Build je OK", problem je 100% browser cache.
