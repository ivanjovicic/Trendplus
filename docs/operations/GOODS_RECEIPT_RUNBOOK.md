# Prijem robe od dobavljača

## Redovan prijem

1. Otvori **Unos robe** i unesi dobavljača, vrstu i broj dokumenta, oba datuma i lokaciju na koju roba fizički stiže.
2. Za postojeći artikal izaberi rezultat pretrage na toj lokaciji. Za novi artikal unesi naziv, tip obuće i sezonu. Unesi količinu i poznate nabavne i prodajne cene u RSD.
3. Pregledaj zbir količina i nabavnih iznosa. Zbir je informativan i ne obračunava PDV ni rabat. Cene stavke prijema ne menjaju automatski cene u šifarniku.
4. Potvrdi ceo dokument jednom serverskom operacijom. Sačuvaj prikazani ID prijema; kasnije ga možeš pronaći u istoriji po ID-ju, dokumentu ili dobavljaču.

Prijem zaključava stanje postojećih artikala u PostgreSQL-u i povećava količinu atomskim upisom. Zaglavlje, stavke i kretanja `Ulaz robe` potvrđuju se zajedno ili se zajedno vraćaju. Analitičko kretanje se projektuje idempotentno preko SyncWorker-a; zastoj projekcije ne poništava niti ponavlja već potvrđenu operativnu zalihu.

Access import blokira batch ako pozitivan izvorni ID artikla zauzima ciljni artikal koji nije porekla `access`; log navodi batch, izvorni i ciljni ID i poreklo bez sirovih podataka artikla. Potrebna je eksplicitna identitetska rekonsilijacija pre ponovnog importa.

## Nepoznat ishod ili sumnjiv dokument

- Ako veza pukne tokom potvrde, ne menjaj stavke. Izaberi **Ponovi isti zahtev i proveri ishod**; isti serverski ključ vraća već sačuvani prijem.
- Ako ponovo otvoriš ekran iz iste navigacije, sistem proverava ključ prijema i prikazuje potvrđeni ID ako je server već sačuvao dokument.
- Upozorenje za isti dobavljački dokument i lokaciju znači da možda postoji raniji prijem. Potvrdi nastavak samo kada je ovo zaista odvojena delimična isporuka. Takav prijem dobija zaseban ID i ključ zahteva.
- Ako se pojavi greška projekcije analitike, prijem ostaje potvrđen u operativnoj bazi. Proveri SyncWorker log `Goods receipt movement projection failed`; ne ponavljaj prijem novim ključem.

## Pogrešan potvrđeni prijem

Istorija prijema je neizmenjiva kroz ovaj ekran i ne postoji tiho brisanje ni ponovno knjiženje. Ova isporuka ne uvodi operaciju storna/korekcije. Dok se ne odobri i implementira namenska korektivna operacija sa povezanim negativnim kretanjem, pogrešan potvrđeni prijem eskaliraj administratoru za kontrolisanu korekciju; ne pokušavaj da ga „ispraviš” ponovnim unosom celog dokumenta.
