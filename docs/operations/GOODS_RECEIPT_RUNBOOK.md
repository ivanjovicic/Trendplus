# Prijem robe od dobavljača

## Redovan prijem

1. Otvori **Unos robe** i unesi dobavljača, vrstu i broj dokumenta, oba datuma i lokaciju na koju roba fizički stiže.
2. Za postojeći artikal izaberi rezultat pretrage na toj lokaciji. Za novi artikal unesi naziv, tip obuće i sezonu. Unesi količinu i poznate nabavne i prodajne cene u RSD.
3. Pregledaj zbir količina i nabavnih iznosa. Zbir je informativan i ne obračunava PDV ni rabat. Cene stavke prijema ne menjaju automatski cene u šifarniku.
4. Potvrdi ceo dokument jednom serverskom operacijom. Sačuvaj prikazani ID prijema; kasnije ga možeš pronaći u istoriji po ID-ju, dokumentu ili dobavljaču.

Prijem zaključava stanje postojećih artikala u PostgreSQL-u i povećava količinu atomskim upisom. Zaglavlje, stavke i kretanja `Ulaz robe` potvrđuju se zajedno ili se zajedno vraćaju. Analitičko kretanje se projektuje idempotentno preko SyncWorker-a; zastoj projekcije ne poništava niti ponavlja već potvrđenu operativnu zalihu.

**Bezbednosni uslov:** stvarno knjiženje i storno su isključeni dok `GoodsReceipts:WritesEnabled` nije eksplicitno `true` na namenski kontrolisanom serveru (podrazumevano je OFF, API vraća 404). Zajednički `X-Admin-Key` i dalje štiti čitanje, ali **nije dovoljan za upis**. Za svaki write zahtev server proverava dodatne zaglavlja `X-Receipt-Operator-Id` i `X-Receipt-Operator-Key` prema zasebno uključenoj, server-side konfiguraciji `GoodsReceipts:Operators:<operator-id>:Enabled=true` i `GoodsReceipts:Operators:<operator-id>:Key`. Svaki operator mora imati svoj nasumičan ključ od najmanje 32 karaktera, različit od zajedničkog admin ključa, koji se obezbeđuje i rotira van Git-a; ne sme se kopirati/deliti između ljudi. Samo verifikacija posebnog operaterskog ključa daje `CreatedBy` / `ReversedBy` identitet operatera. UI ne sme trajno da čuva ove ključeve, a server ne sme da ih loguje. Identitet je auditabilan tek kada administrator stvarno dodeli pojedinačne kredencijale ovlašćenim osobama; sama konfiguraciona šema ne dokazuje ko je fizički unosio podatke.

Primer **naziva** konfiguracionih promenljivih (bez stvarnih tajni): `GoodsReceipts__WritesEnabled`, `GoodsReceipts__Operators__stock_clerk_1__Enabled`, `GoodsReceipts__Operators__stock_clerk_1__Key`. Ne uključivati dok ne prođu testovi, izolovani browser smoke i odobrenje vlasnika.

Access import blokira batch ako pozitivan izvorni ID artikla zauzima ciljni artikal koji nije porekla `access`; log navodi batch, izvorni i ciljni ID i poreklo bez sirovih podataka artikla. Potrebna je eksplicitna identitetska rekonsilijacija pre ponovnog importa.

## Nepoznat ishod ili sumnjiv dokument

- Ako veza pukne tokom potvrde, ne menjaj stavke. Izaberi **Ponovi isti zahtev i proveri ishod**; isti serverski ključ vraća već sačuvani prijem.
- Ako ponovo otvoriš ekran iz iste navigacije, sistem proverava ključ prijema i prikazuje potvrđeni ID ako je server već sačuvao dokument.
- Upozorenje za isti dobavljački dokument i lokaciju znači da možda postoji raniji prijem. Potvrdi nastavak samo kada je ovo zaista odvojena delimična isporuka. Takav prijem dobija zaseban ID i ključ zahteva.
- Ako se pojavi greška projekcije analitike, prijem ostaje potvrđen u operativnoj bazi. Proveri SyncWorker log `Goods receipt movement projection failed`; ne ponavljaj prijem novim ključem.

## Pogrešan potvrđeni prijem: povezani storno

Iz istorije odabrati originalni prijem u statusu `confirmed`, uneti razlog (10–500 znakova) i lične operaterske kredencijale, pa potvrditi **Storniraj potvrđeni prijem**. Server zahteva identičnu admin + operatersku autorizaciju kao za početni upis. `POST /api/goods-receipts/{id}/reverse` prima jedinstveni `requestKey` i `reason`; ponavljanje istog zahteva je idempotentno, a drugi pokušaj sa različitim ključem ili razlogom vraća konflikt.

Storno je jedinstvena PostgreSQL transakcija: zaključava zaglavlje, proverava aktuelno stanje i umanjuje zalihe svih artikala, upisuje potpisana **negativna** kretanja `Ulaz robe`, vezuje svaki storno-dnevnik preko `GoodsReceiptLines.ReversalMovementId` i beleži `reversed`, razlog, autora i vreme na zaglavlju. Originalni prijem, stavke, cene i pozitivna kretanja ostaju neizmenjeni. `GoodsReceiptMovementProjector` naknadno šalje svako storno-kretanje u analitiku idempotentno. Neto potpisana `Kolicina` i `Iznos` moraju vratiti stanje bez dvostrukog knjiženja.

**Namerno ograničenje:** ako je roba već izdata/prodata/preneta tako da raspoloživo stanje nije dovoljno za ceo storno, sistem odbija ceo storno sa 409 i ne menja nijednu stavku. Tada je potreban zaseban poslovno odobren korektivni postupak; nikada ručno ne prepravljati originalne evidencije, Access izvor ili upisivati lažan negativan lager. Novi katalogski artikal nastao kroz prijem ostaje kao istorijski zapis i pri storno-zalihi 0 se ne briše.

## Release/uslov merge-a

Pojedinačni operator, storno i autorizacija moraju biti dokazani na PostgreSQL integracionim testovima i uz izolovan browser desktop/mobile scenario (bez stvarnih zaliha). Dok to nije dokazano i vlasnik nije odobrio kontrolisano uključivanje, `GoodsReceipts:WritesEnabled` ostaje OFF na svim javnim okruženjima. CI i generički Vercel preview nisu dokaz stvarnog knjiženja, prikaza storna niti ispravnosti konkretnog Access stanja.
