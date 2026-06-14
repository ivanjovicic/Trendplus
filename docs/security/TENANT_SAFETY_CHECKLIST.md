# Tenant Safety Checklist

Ovaj dokument opisuje minimalna pravila za budući SaaS model, bez implementacije multi-tenancy-ja u ovom koraku.
Za sada je preporuka da pilot ostane strogo izolovan po kupcu.

## Current pilot recommendation

Trenutno preporučeni model:

- jedan deployment po kupcu
- poseban DB po kupcu kada god je moguće
- poseban storage / file root po kupcu kada god je moguće

Ovo smanjuje rizik pre nego što `TenantId` i potpuna tenant-scope disciplina budu uvedeni kroz ceo sistem.

## Future rule

Svaki query, cache key, report snapshot, export i background job mora da bude `TenantId`-scoped pre bilo kakvog shared SaaS modela.

## Tenant-sensitive areas

- analytics cache keys
- report snapshots
- action queue `sourceKey`
- refresh history
- import files
- logs / error records
- exports
- background jobs

## Checklist table

| Area | Tenant-safe today? | Risk | Future action |
|---|---|---|---|
| Analytics cache keys | No | cache može vratiti podatke drugog kupca ako key nije tenant-scoped | uvesti `TenantId` u svaki analytics cache key i invalidation path |
| Report snapshots | No | snapshot ili durable report može biti pročitan preko pogrešnog scope-a | svaki snapshot mora nositi `TenantId`, storage path i auth proveru |
| Action Queue `sourceKey` | Partial | `sourceKey` može biti stabilan, ali nije dovoljan bez tenant dimenzije | proširiti source identitet tenant scope-om i proverama pri upsert/read operacijama |
| Refresh history | No | refresh status i istorija mogu pomešati događaje više kupaca | odvojiti refresh istoriju i status po tenant-u |
| Import files | No | upload i staging fajlovi mogu završiti u zajedničkom prostoru | izolovati storage root i naming convention po tenant-u |
| Logs / error records | Partial | correlation i error summary mogu sadržati tenant-sensitive reference | dodati tenant oznaku i masking pravila gde treba |
| Exports | No | eksportovani CSV/PDF može završiti u pogrešnom storage ili download scope-u | tenant-scope storage, metadata i authorization za svaki export |
| Background jobs | No | worker može obraditi pogrešan dataset ako job nema tenant identitet | svaki job mora nositi `TenantId` i koristiti tenant-scoped dependencies |

## Operational guidance before SaaS

- Ne koristiti shared deployment za više kupaca bez pune tenant-scope revizije.
- Ne koristiti shared file storage bez odvojenih root putanja.
- Ne koristiti shared cache namespace bez tenant prefiksa.
- Incident i audit tragovi moraju biti povezivi sa tačnim kupcem.

## Open gaps

- `TenantId` nije sistemski uveden kroz query/cache/report/job slojeve
- export i snapshot storage nisu formalno tenant-scoped u ovom dokumentu kroz implementaciju
- log retention i audit masking pravila traže dodatnu bezbednosnu razradu
