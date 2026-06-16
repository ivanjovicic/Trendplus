# Analytics Stabilization Review

Datum: 2026-06-16  
Repo: `ivanjovicic/Trendplus`  
Scope: inventory trust signal, data quality empty states, analytics navigation, action outcome summary backend/frontend surface

## Cilj

Ovaj review proverava da li poslednji analytics trust/navigation/outcome commit-i ostaju:

- mali i konzistentni sa queue ciljevima
- usklađeni sa no-fake-zero pravilima
- bez novih broad refactor-a
- dovoljno stabilni za dalji pilot polish

## Kratak zaključak

Nisu pronađene male regresije koje traže novi code fix u ovom koraku.

Promene su ostale uske i u skladu sa prethodnim queue auditima:

- inventory quality signal više ne deluje lažno zdravo kada postoji warning/partial signal
- data quality i pilot intake više ne tretiraju missing/null payload kao “nema problema”
- canonical analytics entry point-i su vidljivi u sidebar-u, uz zadržane legacy rute
- action outcome summary ostaje read-only i ne ruši listu akcija kada summary poziv padne

## Nalazi po oblasti

| Area | Provera | Nalaz | Status |
|---|---|---|---|
| Inventory | quality kartica ne sme ostati `podaci OK` kada postoji warning/partial/insufficient signal | `InventoryPage` računa `dataQualityNeedsReview`, a `DecisionSummaryBar` tada prikazuje `Kvalitet podataka traži proveru` | OK |
| Inventory | data quality link treba da vodi na `/analytics/data-quality` | `DecisionSummaryBar` prima `dataQualityHref` i renderuje `Link` ka `/analytics/data-quality` | OK |
| Data Quality | all-zero issue state treba da bude pozitivan | `PilotDataQualityIntakeReport` prikazuje validno prazno stanje kada su svi issue/impact signali eksplicitno nula | OK |
| Data Quality | missing/null/partial ne sme delovati čisto | helper signal state vraća `partial`, a UI prikazuje neutralno upozorenje umesto clean stanja | OK |
| Navigation | `/analytics/products` vidljiv kao `Odluke o proizvodima` | prisutno u `navConfig.ts` | OK |
| Navigation | `/analytics/supplier` vidljiv kao `Pregled dobavljača` | prisutno u `navConfig.ts` | OK |
| Navigation | `/analytics/actions` vidljiv kao `Centralne akcije` | prisutno u `navConfig.ts` | OK |
| Navigation | legacy rute ne smeju nestati | legacy/exploratory analytics rute ostaju prisutne, a route smoke prolazi | OK |
| Action Outcome Summary | endpoint mora da postoji | `GET /api/analytics/actions/outcomes/summary` postoji u `AnalyticsActionsEndpoints` | OK |
| Action Outcome Summary | `pending` nije failure | service summary računa `pending` van success/negative denominator-a | OK |
| Action Outcome Summary | `measuredImpactRsd = null` je unknown, ne zero | service vraća `null` sum/ratio kada nema merljiv denominator | OK |
| Action Outcome Summary | summary failure ne sme slomiti listu akcija | `AnalyticsActionsPage` prikazuje non-blocking summary error i ostavlja listu funkcionalnom | OK |

## Pregled konzistentnosti sa queue ciljevima

### Inventory trust / clarity

- Nalaz iz `INVENTORY_UX_AUDIT` je zatvoren uskom frontend izmenom.
- Nije dirana inventory algoritmika.
- Trust warning je sada propagiran do top-level decision kartice.

### Data Quality trust

- Nema više implicitnog pretvaranja `null` u `0` za trust-kritične issue/impact signale.
- Real zero ostaje pozitivan signal.
- Partial payload ostaje neutralno upozorenje, ne fake clean stanje.

### Navigation clarity

- Canonical pilot flow je sada vidljiv iz sidebar-a.
- Legacy rute nisu uklonjene.
- Dashboard/route smoke standard ostaje netaknut.

### Action outcome summary

- Backend contract i frontend read-only surface rade u skladu sa Phase 1 planom.
- Trenutna implementacija je i dalje šira od minimalnog spec-a, ali bez konflikta za ovaj review.
- Summary surface je izolovan od glavne liste i ne pretvara failure u broken page stanje.

## Testovi i provere korišćeni u review-u

### Ciljani frontend testovi

- `npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx`
- `npm run test -- --run src/pages/__tests__/AnalyticsActionsPage.spec.tsx`
- `npm run test -- --run src/components/analytics/__tests__/AnalyticsMethodologyRegistry.spec.tsx`
- `npm run test -- --run src/pages/__tests__/InventoryPage.queueStatus.spec.tsx`

### Šta je posebno potvrđeno

- route smoke i compatibility analytics rute
- non-blocking summary fallback za action outcome panel
- zero-vs-partial trust ponašanje za pilot intake
- inventory page surface i queue status render bez regresije

## Otvoreni rizici

1. `Action Outcome Summary` spec je uži od trenutne implementacije:
   - kod već podržava `resolvedFrom` / `resolvedTo`
   - kod već vraća `byConfidenceBucket` i `byReliabilityBucket`
   - to nije regresija, ali ostaje contract hygiene follow-up

2. Inventory quality warning je sada ispravan, ali nema poseban mali component-level regression test za `DecisionSummaryBar`.

3. `vite build` i dalje prijavljuje postojeći chunk-size warning, bez novih grešaka.

## Preporučeni sledeći mali koraci

1. Dodati mali backend test follow-up za `resolvedFrom/resolvedTo` validation i extra summary cohort bucket-e.
2. Po želji dodati mali `DecisionSummaryBar` component test za warning + Data Quality link.
3. Zadržati summary frontend kao read-only dok access-control P0 gapovi ne budu rešeni.
