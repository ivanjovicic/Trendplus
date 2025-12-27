# Logs Feature - Pregled Serilog Logova

## ?? Opis

Dodata je stranica za pregled svih logova iz Serilog-a sa mogu?nostima filtriranja i sortiranja.

## ? Mogu?nosti

- **Filtriranje po nivou loga**: Debug, Information, Warning, Error, Fatal
- **Filtriranje po datumu**: Od i do odre?enog vremena
- **Paginacija**: 50 logova po stranici
- **Sortiranje**: Najnoviji logovi prikazuju se prvi
- **Detalji exceptiona**: Klik na exception prikazuje ceo stack trace
- **Color-coded nivoi**: Razli?ite boje za razli?ite nivoe važnosti

## ?? Pristup

Stranica je dostupna na:
- URL: `/logs`
- Link u navigaciji: "?? Logovi"
- Link na po?etnoj stranici

## ?? Vizuelne indikacije

- ?? **Error/Fatal**: Crvena boja, crvena pozadina
- ?? **Warning**: Narandžasta boja, žuta pozadina  
- ?? **Information**: Plava boja, svetloplava pozadina
- ? **Debug**: Siva boja, bela pozadina

## ?? Backend

- **Endpoint**: `GET /api/logs`
- **Query parametri**:
  - `pageNumber` (default: 1)
  - `pageSize` (default: 100)
  - `level` (optional: Debug, Information, Warning, Error, Fatal)
  - `fromDate` (optional: ISO datetime)
  - `toDate` (optional: ISO datetime)

## ?? Log Fajlovi

Logovi se ?uvaju u `Logs/` folderu sa rolling interval po danu:
- Format: `log-YYYY-MM-DD.txt`
- Konfigurisano u `appsettings.json`

## ?? Koriš?enje

1. Kliknite na "?? Logovi" u navigaciji
2. Izaberite filter po nivou ako želite da vidite samo odre?ene tipove logova
3. Izaberite datum range ako želite da filtrirate po vremenu
4. Kliknite na "Exception Details" da vidite ceo stack trace
5. Koristite "Prethodna" i "Slede?a" dugmad za paginaciju

## ?? Napomene

- Logovi se u?itavaju direktno iz fajlova na serveru
- Paginacija je optimizovana za brze preglede
- Exception details su collapsible za bolju preglednost
