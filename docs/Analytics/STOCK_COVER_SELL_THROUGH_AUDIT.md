# Stock Cover / Sell-through Audit

| Layer | Stock cover | Sell-through | Gap | Action |
|---|---|---|---|---|
| Backend formula | Yes | Partial | Sell-through formula je backend-calculated, ali denominator u trenutnim endpointima cesto nema opening/inbound ulaze pa signal pada na insufficient_data (ispravno), bez eksplicitnog recommendationAllowed polja u DTO pre izmene. | Zadrzana no-fake-zero logika; dodat `recommendationAllowed` i canonical label polja iz backend-a. |
| DTO | Partial | Partial | Nedostajali `stockCoverStatusLabel`, `sellThroughStatusLabel`, `recommendationAllowed` u inventory/product decision contract-u. | DTO contract prosiren na backend-u i frontend types uskladjeni. |
| Frontend table | Partial | Partial | Prikaz null vrednosti je bio uglavnom "Nedovoljno podataka" i kada signal nije eksplicitno insufficient_data. | Dodata razlika: `insufficient_data -> Nedovoljno podataka`, `null bez insufficient_data -> Nije dostupno`; backend label se koristi kada postoji. |
| KPI cards | Yes | Yes | KPI explain postoji, ali sell-through metodologija nije bila dovoljno precizna za denominator/blocked cases. | Metodologija za sell-through unapredjena u metric definitions. |
| Methodology | Yes | Partial | Formula za sell-through bila generalna i bez jasnog blocked/no-fake-zero pravila. | Formula i blocked uslovi sada eksplicitno dokumentovani (`soldUnits / (openingStockUnits + inboundUnits)`). |
| Tests | Partial | Partial | Backend testovi postoje, ali bez provere novih contract polja; frontend nije imao direktne testove za signal->action mapiranje i null/unavailable razliku. | Dodati backend assertion-i i novi frontend testovi za mapiranje (`REPLENISH`/`SLOW_STOCK_REVIEW`/`SIGNAL_REVIEW`) i prikaz unavailable stanja. |
| Action Queue | Partial | Partial | Inventory je imao signal mapiranje; Product Decision je uglavnom sledio recommendation status bez eksplicitnog signal status mapiranja. | Product Decision queue mapiranje dopunjeno canonical signal pravilima; metadata prosirena signal poljima. |
