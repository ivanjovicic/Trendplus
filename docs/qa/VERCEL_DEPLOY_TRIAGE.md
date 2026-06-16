# Vercel Deploy Triage

Datum: 2026-06-16
Repo: `ivanjovicic/Trendplus`
Status context: `Vercel`

## TL;DR

Vercel failure je klasifikovan kao **Vercel project root / build configuration issue**, ne kao analytics frontend build regresija.

Najverovatniji uzrok:

- Vercel projekat pokušava da gradi iz **repo root-a**
- repo root ima `package.json`, ali **nema** `build` skriptu
- stvarni frontend koji prolazi build nalazi se u `Klijent/clientapp`
- `vercel.json` postoji samo u `Klijent/clientapp/vercel.json`, a Vercel dokumentacija očekuje da config bude u **project root directory**

## Šta je provereno

### 1. GitHub/Vercel status

GitHub status za novije commitove pokazuje isti tip pada:

- `783adbc` -> `Vercel: failure`
- `b4d579f` -> `Vercel: failure`

User-provided SHA `7fb6e04` nije pronađen u trenutnom `ivanjovicic/Trendplus` repou, pa je verovatno zastareo ili pogrešno skraćen SHA. Pošto isti Vercel failure postoji i na aktuelnom `main`, triage je ipak validan i aktuelan.

### 2. Lokalna reprodukcija

Repo root:

```powershell
cd c:\Users\Alex\source\repos\TrendplusNew
npm run build
```

Rezultat:

```text
npm error Missing script: "build"
```

Frontend app root:

```powershell
cd c:\Users\Alex\source\repos\TrendplusNew\Klijent\clientapp
npm run check:analytics-guardrails
npm run build
```

Rezultat:

- guardrails: PASS
- Vite production build: PASS

## Klasifikacija

Kategorija:

- `Vercel project root/output config`

Nije:

- `npm install/dependency`
- `TypeScript/build`
- `guardrail`
- `missing env variable`
- `external/transient`

## Dokazi

### Repo layout

- repo root `package.json` postoji, ali nema `build` skriptu
- frontend `package.json` sa validnim `build` i `check:analytics-guardrails` skriptama je u:
  - `Klijent/clientapp/package.json`
- Vercel routing config je u:
  - `Klijent/clientapp/vercel.json`

### Relevantna Vercel dokumentacija

Vercel docs potvrđuju:

- `vercel.json` treba da bude u **project root directory**
- `Root Directory`, `Build Command`, `Install Command` i `Output Directory` se podešavaju kroz Project Settings ili root-level config

Reference:

- Vercel Project Configuration: https://vercel.com/docs/project-configuration
- Vercel `vercel.json`: https://vercel.com/docs/project-configuration/vercel-json
- Vercel General Settings / Root Directory: https://vercel.com/docs/project-configuration/general-settings

## Tačan fix u Vercel-u

U Vercel dashboard-u za projekat `trendplus`:

1. Otvoriti `Project Settings`
2. Otvoriti `Build and Deployment`
3. Postaviti `Root Directory` na:

```text
Klijent/clientapp
```

4. Proveriti ili eksplicitno setovati:

```text
Framework Preset: Vite
Install Command: npm ci
Build Command: npm run build
Output Directory: dist
```

5. Redeploy poslednji commit

## Zašto je ovo ispravan fix

Kad je `Root Directory = Klijent/clientapp`:

- Vercel koristi pravi `package.json`
- `npm run build` postoji i prolazi
- `Klijent/clientapp/vercel.json` postaje config u project root-u za taj deploy
- output `dist/` odgovara stvarnom Vite build-u

## Šta nije menjano

- nema izmene analytics logike
- nema izmene UI feature-a
- nema izmene backend-a

## Ako deploy i dalje padne posle Root Directory fix-a

Sledeći check redosled:

1. potvrditi da je Vercel zaista snimio `Root Directory = Klijent/clientapp`
2. potvrditi da `Install Command` nije override-ovan na repo root vrednost
3. potvrditi da `Output Directory` nije override-ovan na pogrešnu putanju
4. tek onda proveravati env vars ili transient platform issue

## Zaključak

Failure reason je poznat:

- **Vercel build je usmeren na pogrešan root**

To je deployment configuration issue, ne frontend regression u latest analytics commit-u.
