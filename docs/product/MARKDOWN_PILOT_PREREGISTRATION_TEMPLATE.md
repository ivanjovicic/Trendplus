# Pre-registracija markdown pilot kohorte

Status dokumenta: obrazac za popunjavanje pre izvršenja cena. Prazna polja nisu dokaz da pilot postoji.

Ovaj obrazac registruje opisno praćenje unapred izabranih markdown akcija. Ne definiše uzročnu atribuciju niti dokaz da je promena cene proizvela ishod. Rezultat se naziva **posmatrani ishod**.

## Vlasnik i vremenski pečat

| Polje | Vrednost |
|---|---|
| Vlasnik kohorte | |
| Datum/vreme odobrenja (UTC) | |
| SHA dokumenta pre prve akcije | |
| Izvor Pre-Nivelacija prioriteta i period podataka | |
| Osnova troška | Po trenutno dostupnom istorijskom trošku; RQ601 verdict: |
| Izvor/period i poslednji uspešan import | |

## Kohorta pre markdown-a

- Izabrati 20–30 različitih SKU×prodavnica iz retail populacije `Trend PLUS 1` / `Trend PLUS 2` koje je Pre-Nivelacija označio kao dozvoljene za odluku.
- Svaka akcija mora biti zabeležena pre izvršenja cene: SKU/artikal ID, prodavnica, cena pre, planirana cena posle, datum/vreme početka, razlog, vlasnik i izvorni događaj/odobrenje.
- Zadržati sve unapred izabrane akcije u analizi. Ne uklanjati akciju zbog toga što joj prodaja ili marža nisu povoljne.

| Action ID | Artikal ID / PLU | Prodavnica | Dobavljač | Tip obuće | Starosni pojas zalihe na izboru | Cena pre | Planirana cena posle | Planirani datum | Izvor / razlog |
|---|---|---|---|---|---|---:|---:|---|---|
| | | | | | | | | | |

## Uparena poredbena grupa

- Za svaki tretirani SKU izabrati najmanje jedan SKU koji nije markdown-ovan u istom periodu, iz istog dobavljača, tipa obuće i prodavnice, u istom unapred definisanom starosnom pojasu zalihe.
- Predloženi pojasevi za ovaj obrazac su `0–30`, `31–90`, `91–180`, `181+` dana i `unknown`; evidentirati izvor datuma prijema. Ako datum/starost nije poznata, ne predstavljati `unknown` kao upareno sa poznatim pojasom.
- Sačuvati sve kandidate i razlog izbora. Ako nema valjanog para, zabeležiti ga kao neuparenog; ne menjati kriterijume nakon gledanja ishoda.

| Action ID | Kontrolni artikal ID / PLU | Prodavnica | Dobavljač | Tip obuće | Starosni pojas | Nije markdown-ovan u prozoru? | Izvor atributa i datum |
|---|---|---|---|---|---|---|---|
| | | | | | | | |

## Zaključana definicija merenja

- Period pre: 28 kalendarskih dana u zoni `Europe/Belgrade`, `[datum akcije − 28 dana, datum akcije)`.
- Period posle: 28 kalendarskih dana u zoni `Europe/Belgrade`, `[datum akcije, datum akcije + 28 dana)`.
- Količina: potpisani zbir pari sa prodajnih stavki; povrati ostaju negativni i vidljivi. Računi `DUG` i `KOREKCIJA` se isključuju prema postojećem ledger ugovoru.
- Realizovana bruto marža (RSD): `Σ((prodajna cena po paru − istorijski trošak po paru) × potpisana količina)`. Ako postoji stavka sa nepoznatim troškom ili nema prodajnih stavki, ukupan iznos je `N/A`; poznati deo i broj nepoznatih stavki prikazuju se odvojeno. Nepoznato se nikad ne pretvara u nulu.
- Preostala zaliha: trenutno poznato stanje iz izvora pri izvršenju upita, uz timestamp izvora. Nije istorijsko stanje na kraju prozora.
- Za poredbene SKU prikazati iste metrike i prozore. Prikazati i neuparene zapise uz `match_status`; ne uključivati ih u tvrdnju o uparenoj grupi.

## Isključenja i dokazni uslovi

- Isključiti smoke/test artikle, duplikatne SKU×prodavnica akcije, ne-retail prodavnice, prodavnice bez pouzdanog identiteta, price event bez dokazane markdown orijentacije i stavke računa `DUG`/`KOREKCIJA`.
- Prikazati pokrivenost istorijskog troška, nepoznat dobavljač/tip/prodavnica/starost, nedostajuću cenovnu istoriju i uparivanje koje nije zadovoljilo sva četiri atributa.
- SQL dry-run mora navesti tačan izvor, uvezeni opseg i vreme izvršenja. Sintetički fixture nije istorijski import.
- Ne tvrditi uzročnost, inkrementalnu dobit ili da je pilot potvrdio politiku cene. Bez randomizacije i istorijske dnevne zalihe ostaju selekcioni bias i ograničenje zalihe.

## Odluke pre prve akcije

| Odluka | Vlasnik | Datum | Odluka / dokaz |
|---|---|---|---|
| Osnova i pokrivenost troška | | | |
| Starosni pojas / dokaz prijema | | | |
| Neuparen kandidat i postupanje | | | |
| Prodajni povrati i korekcije | | | |
| Freshness i dozvoljeni retail opseg | | | |

Ovaj obrazac mora biti popunjen i sačuvan pre prve izvršene akcije. Svako kasnije odstupanje čuva se kao datirana dopuna; original se ne prepisuje.
