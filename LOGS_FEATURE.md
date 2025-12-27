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

## ?? Pokretanje (nakon izmena)

### Backend
```bash
cd Trendplus2
dotnet run
```

### Frontend
```bash
cd Klijent/clientapp
npm run dev
```

**VAŽNO:** Nakon dodavanja novih endpointa ili izmene backend koda, morate restartovati backend server!

## ?? Troubleshooting

### 404 Error na `/api/logs`

Ako dobijete 404 error:

1. **Proverite da li backend radi:**
   ```bash
   curl http://localhost:8080/health
   ```
   
2. **Restartujte backend:**
   - Zaustavite trenutni proces (Ctrl+C)
   - Ponovo pokrenite: `cd Trendplus2 && dotnet run`

3. **Proverite da li endpoint postoji:**
   ```bash
   curl http://localhost:8080/api/logs?pageNumber=1&pageSize=10
   ```

4. **Restartujte frontend dev server:**
   - Zaustavite trenutni proces (Ctrl+C)
   - Ponovo pokrenite: `cd Klijent/clientapp && npm run dev`

### Logovi se ne prikazuju

- Proverite da li postoji `Trendplus2/Logs` folder
- Proverite da li postoje `.txt` fajlovi u folderu
- Proverite regex format u `GetLogsHandler.cs`

## ?? Napomene

- Logovi se u?itavaju direktno iz fajlova na serveru
- Paginacija je optimizovana za brze preglede
- Exception details su collapsible za bolju preglednost
- ?ita se maksimalno 10 najnovijih log fajlova da bi se izbegli performance problemi
