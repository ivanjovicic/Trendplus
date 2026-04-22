Radiš kao **senior full-stack / frontend infrastructure engineer** na postojećoj aplikaciji.

Treba da implementiraš **production-ready client-side API fallback mehanizam** tako da frontend koristi:

- **Fly** kao **primarni API**
- **Render** kao **fallback API**

## Kontekst

Trenutno je stanje sledeće:

- frontend production build koristi `VITE_API_BASE_URL`
- trenutno je primarni endpoint podešen na **Fly**
- Render više nije automatski deploy target, već postoji samo kao **manuelni fallback workflow**
- trenutno **ne postoji runtime fallback** u frontend kodu
- ako Fly padne, frontend **ne prelazi automatski** na Render

Cilj je da se to promeni tako da frontend može **u runtime-u** da pređe sa Fly na Render kada Fly nije dostupan.

---

# GLAVNI CILJ

Implementiraj **client-side fallback** u frontend aplikaciji.

Frontend treba da:

1. koristi `VITE_API_BASE_URL` kao primarni API
2. koristi novi `VITE_API_FALLBACK_URL` kao rezervni API
3. pri mrežnoj grešci / timeout-u / nedostupnosti primarnog API-ja automatski pređe na fallback
4. ne prebacuje se na fallback za obične business greške tipa 400/401/403/404
5. pamti aktivni API host tokom sesije da ne pokušava svaki put iz početka
6. po mogućju periodično proverava da li se primarni API oporavio i može da se vrati na njega
7. ostane jednostavan, robustan i bez scope creep-a

---

# ŠTA TAČNO ŽELIM DA IMPLEMENTIRAŠ

## 1. Novi env config
Dodaj podršku za sledeće varijable:

- `VITE_API_BASE_URL` → primarni API (Fly)
- `VITE_API_FALLBACK_URL` → fallback API (Render)

Obavezno ažuriraj:
- `.env.production.example`
- ako treba i druge env fajlove / docs gde je smisleno

Nemoj hardkodovati URL-ove u kodu.

---

## 2. Centralizovan API URL / HTTP fallback layer
Pronađi postojeći shared API layer, npr:
- `apiUrl.ts`
- zajednički `http client`
- `fetch wrapper`
- axios wrapper ako postoji

Tu implementiraj fallback logiku, a ne po pojedinačnim ekranima.

Ako postoji više mesta koja grade API URL, refaktoriši minimalno da postoji **jedna centralna tačka odlučivanja**.

---

## 3. Pravila fallback ponašanja
Implementacija treba da radi ovako:

### Primarni flow
- pokušaj primarni `VITE_API_BASE_URL`

### Prebaci na fallback samo u sledećim slučajevima:
- network error
- fetch failed
- timeout
- connection refused
- DNS resolution failure
- 502 / 503 / 504
- health check failure
- eksplicitno nedostupan host

### Ne prebacuj na fallback za:
- 400
- 401
- 403
- 404
- 422
- druge business / auth greške gde je server dostupan ali je zahtev loš

Drugim rečima:
**fallback samo kada je problem u dostupnosti API-ja**, ne kada je problem u zahtevu ili autorizaciji.

---

## 4. Health-check / availability provera
Ako backend već ima health endpoint, iskoristi njega.  
Ako ne postoji jasno centralizovan health endpoint, pronađi najlakšu postojeću sigurnu rutu za proveru dostupnosti.

Poželjno:
- prvo proveri primarni API kroz health endpoint
- ako ne odgovara ili timeout-uje, prebaci na fallback
- kada fallback postane aktivan, ne spamuj health na svakom request-u

Uvedi razuman pristup, npr:
- in-memory current active base
- optional localStorage/sessionStorage persistence
- cooldown za ponovno probe-ovanje primarnog API-ja
- periodičan retry povratka na primary, ali ne agresivno

---

## 5. State / memorisanje aktivnog hosta
Implementiraj da aplikacija zna koji host trenutno koristi.

Poželjno:
- aktivni host čuvaj u memoriji
- opciono čuvaj i u `localStorage` ili `sessionStorage`
- dodaj TTL ili “last failure time” mehanizam ako pomaže

Cilj:
- da se aplikacija ne vraća slepo na Fly pri svakom pojedinačnom request-u ako je Fly trenutno nedostupan
- da fallback bude stabilan, a ne haotičan

---

## 6. Povratak na primarni API
Ako aplikacija pređe na Render fallback, ne želim da tu ostane zauvek bez pokušaja oporavka.

Implementiraj razumnu strategiju:
- nakon određenog vremena ili broja pokušaja proveri da li je Fly opet zdrav
- ako jeste, vrati aktivni API nazad na primarni
- ovo uradi oprezno, bez loop-ova i bez flicker ponašanja

Poželjna logika:
- cooldown / retry window
- npr. retry probe nakon X minuta ili pri sledećem većem request-u
- minimalan i stabilan “circuit-breaker-like” pristup

Nemoj praviti prekomplikovan sistem, ali nemoj ni ostaviti fallback zauvek zalepljen.

---

## 7. Logging / diagnostics
Dodaj minimalno korisne logove / debug signalizaciju.

Poželjno:
- u dev modu ili debug modu loguj:
  - da je primarni API pao
  - da je fallback aktiviran
  - koji host je aktivan
  - da li je vraćen primary
- nemoj zatrpati konzolu
- nemoj izlagati osetljive podatke

Ako već postoji logger utility, koristi njega.

---

## 8. UI ne treba veliki redizajn
Ne želim veliki UI rad, ali ako ima smisla možeš dodati **vrlo mali, diskretan** indikator / hook / debug helper da se zna koji API je aktivan.

To uradi samo ako je:
- korisno
- minimalno
- bez narušavanja UI-ja

Ako nije potrebno, preskoči.

---

## 9. Testability
Implementaciju napravi tako da bude testabilna.

Poželjno:
- unit test za fallback odluku
- test da 5xx / network error prebacuje na fallback
- test da 4xx NE prebacuje na fallback
- test da se active host pamti
- test da postoji povratak na primary kada se oporavi

Ako projekat nema test infrastrukturu za ovo, onda barem ostavi kod strukturiran tako da testovi mogu lako da se dodaju.

---

# ŠTA NE TREBA DA RADIŠ

Nemoj:
- menjati backend business logiku
- uvoditi DNS failover u ovom tasku
- menjati CI/CD workflow osim ako je baš neophodno za env var dokumentaciju
- širiti scope na deploy automatiku
- uvoditi ogromne biblioteke samo za ovo
- lomiti postojeći API contract
- praviti fallback per-ekran ili per-feature
- hardkodovati URL-ove

Ovo treba da bude:
- minimalno invazivno
- centralizovano
- robustno
- production-safe

---

# OČEKIVANI FAJLOVI ZA IZMENU

Verovatno će biti potrebno menjati nešto poput:

- `apiUrl.ts`
- shared `http client`
- neki `fetch` wrapper
- `.env.production.example`
- eventualno docs / config helper
- eventualno test fajl za API client

Ako u projektu postoji bolja centralna tačka za ovakvu logiku, koristi nju.

---

# OČEKIVANO PONAŠANJE POSLE IMPLEMENTACIJE

Želim sledeći rezultat:

### Scenario A — Fly radi
- frontend koristi Fly
- fallback se ne koristi
- nema nepotrebnog prebacivanja

### Scenario B — Fly padne
- frontend automatski pređe na Render
- korisnik ne mora ručno da reloaduje ili menja config
- aplikacija nastavlja da radi preko fallback-a

### Scenario C — Fly se oporavi
- frontend razumno detektuje oporavak
- vraća se na Fly
- bez haotičnog prebacivanja tamo-vamo

### Scenario D — API vrati 401/403/404
- frontend NE prebacuje na Render
- jer to nije availability problem

---

# OUTPUT KOJI ŽELIM OD TEBE

Vrati rezultat u sledećoj strukturi:

## 1. Kratak plan
- koje fajlove menjaš
- gde uvodiš fallback logiku
- kako rešavaš active host state

## 2. Implementacija
- konkretne izmene u kodu
- fallback mehanizam
- timeout / error handling logika
- recovery to primary logika

## 3. Env/config changes
- šta si dodao za `VITE_API_FALLBACK_URL`
- koje env fajlove si ažurirao

## 4. Pravila ponašanja
jasno napiši:
- kada se ide na fallback
- kada se NE ide na fallback
- kada se vraća na primary

## 5. Test / verification koraci
tačno napiši kako da proverimo:
- Fly down → fallback radi
- Fly up → primary radi
- 4xx ne triggeruje fallback
- recovery nazad na Fly radi

## 6. Build safety
- bez menjanja backend logike
- bez scope creep-a
- bez razbijanja postojećeg ponašanja

---

# DODATNA SMERNICA

Ako moraš da biraš između:
- “minimalno koda ali nepouzdano”
- i
- “malo više logike ali stabilno i production-safe”

izaberi:
- **stabilno**
- **jasno**
- **centralizovano**
- **lako za održavanje**

Želim implementaciju koja izgleda kao da ju je radio iskusan inženjer, ne kao privremeni hack.
