# Frontend Routing And Smoke Test Standards

Datum: 2026-05-26
Repo: ivanjovicic/Trendplus
Scope: App routing, compatibility routes, lazy loading, smoke verifikacije.

## 1) Frontend routing standard

Obavezna pravila:
- App.tsx mora da zadrzi lazy loading za page komponente.
- Ne uklanjati `lazy(() => import(...))` i `Suspense` samo da bi test bio laksi.
- Route smoke test mora da se prilagodi lazy komponentama, ne obrnuto.
- Ako test ne radi sa lazy importima, popraviti test/mocks; ne ukidati lazy loading.
- `RouteFallback` mora da ostane za lazy-loaded routes.

Agent guardrail (obavezno):
- Ne menjaj App.tsx iz lazy/Suspense u direktne import-e radi testa.

## 2) Legacy i compatibility rute

Sledece rute ne uklanjati bez eksplicitne odluke i replacement plana:
- `/analytics/product-decision-center` -> redirect na `/analytics/products`
- `/analytics/data-quality/pilot-intake-report` -> `PilotIntakeReportPage`
- `/admin/configuration`
- `/configuration` -> redirect na `/admin/configuration`
- `/analytics/supplier-sales-stats` (ako se koristi)
- `/analytics/dobavljaci-tipovi-obuce` (ako se koristi)
- `/analytics/supplier-decision-hub` (ako se koristi)

Ako se ruta uklanja:
- mora postojati replacement ruta
- mora postojati redirect ako su linkovi vec koristeni
- mora se azurirati route smoke test
- mora se azurirati production readiness checklist

## 3) Zabrana dupliranih ruta

Pravila:
- Ista ruta ne sme biti definisana dva puta u `App.tsx`.
- Pre merge-a proveriti duplikate posebno za:
  - `/settings/themes`
  - `/analytics/*`
  - `/admin/*`
  - `/configuration`
- Ako postoji alias/redirect, mora biti eksplicitno oznacen kao redirect, ne duplikat iste stranice bez komentara.

## 4) Default theme safety

Pravila:
- Ne menjati `ThemeProvider defaultTheme` u commitovima koji nisu eksplicitno theme/design-system taskovi.
- Route test, smoke test, analytics test ili report task ne sme slucajno menjati default temu.
- Ako se default tema menja:
  - navesti razlog u PR opisu
  - proveriti glavne ekrane
  - azurirati screenshot/smoke checklist ako postoji (`docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`)
  - dokumentovati promenu u theme standardu

## 5) Route smoke test vs browser smoke

Razlika:
- Route smoke test proverava da su rute mapirane i da ne pucaju pri renderu.
- Browser/manual smoke proverava realno ponasanje u browseru: URL refresh, API loading, empty/error stanja i copy.
- Route smoke nije dovoljan za pun production sign-off.

Obavezni route smoke test URL-ovi:
- `/analytics`
- `/analytics/products`
- `/analytics/supplier`
- `/analytics/inventory`
- `/analytics/data-quality`
- `/analytics/actions`
- `/analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30`
- `/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30`

Komanda:

```powershell
cd Klijent/clientapp
npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx
```

Obavezni browser/manual smoke pre production sign-off:
- otvoriti direktan URL
- uraditi browser refresh
- proveriti da nema blank screen-a
- proveriti da nema mojibake-a
- proveriti da empty/error state ne prikazuje lazne nule
- proveriti da report bez payload-a ne prikazuje export dugmad

## 6) Redosled rada

Preporuceni redosled za stabilizacione taskove:
1. Prvo popraviti routing/lazy/legacy/admin rute.
2. Zatim azurirati status dokument.
3. Zatim dodati browser/manual smoke.
4. Tek posle toga nastaviti sa novim business funkcijama (npr. Stock Cover, Sell-through, Supplier Negotiation Pack, Action Queue integracija).

## 7) PR checklist (routing i smoke)

Pre merge-a proveriti:
- App.tsx i dalje koristi lazy/Suspense.
- Nema dupliranih ruta.
- Legacy/admin rute nisu uklonjene bez redirect-a.
- Default theme nije promenjen slucajno.
- Route smoke test prolazi:

```powershell
cd Klijent/clientapp
npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx
```

- `npm run build` prolazi.
- `npm run check:analytics-guardrails` prolazi.

## 8) Visual regression (premium UI)

Za layout/theme/table/header/sidebar izmene koristiti:

- `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`
- evidence template: `docs/qa/ANALYTICS_UI_VISUAL_REVIEW_EVIDENCE_TEMPLATE.md`

Route smoke nije zamena za visual review.
