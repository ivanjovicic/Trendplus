Cilj
Dodati capture SQL upita (query strings) u perfomance logove kako bismo mogli videti sve izvršene upite, proceniti kompleksnost i pronaći prilike za pojednostavljenje/optimizaciju.

Motivacija
- Brzo identifikovati spore i kompleksne SQL upite.
- Korelirati vreme izvršenja sa konkretnim SQL tekstom i parametrima.
- Omogućiti analizu i refaktor upita bez potrebe da ručno reprodukujemo svaki slučaj.

Opseg
- Pokriva server-side query-e koji se izvršavaju u `Api/` projektu (Npgsql, Odbc, ADO.NET), i ključne helper-e koji se već koriste u `AccessImportService`, `SupplierDecisionHub`, i centralne DB wrapper-e.
- Ne upisivati OOM/ogromne tekstove direktno bez truncation/sampling.

Opcije implementacije (kratko)
1) Brza, minimalna (Preporučeno kao prvi korak)
   - Dodati `logger.LogDebug/LogInformation` u postojeće centralne DB helper-e: `ExecuteAnalyticsNonQueryAsync`, `ExecuteSupplierRowsQueryAsync`, `ExecuteSqlCommandAsync`.
   - Logovati: SQL (truncirano na configurable n znakova), sažetak parametara (ili hash), trace/request id.
   - Prednosti: brzo, malo rizika, obuhvata većinu import/analytics poziva.
2) Centralizovano i trajno (bolje za dugoročno)
   - Implementirati `DbCommandInterceptor` za Npgsql/EF Core ili wrapper oko `DbConnection`/`DbCommand` koji emitira događaje (DiagnosticSource/Activity).
   - U `PerformanceLoggingBehavior` (MediatR) uskladiti/attach-ovati prikupljene DB događaje za trenutni request i persistirati u `PerformanceLog`.
   - Prednosti: centralno, konzistentno, ne zahteva menjanje svih pozivnih mesta.
   - Nedostaci: zahteva izmene modela i migracije, veći napor.

Detaljan plan — minimalna implementacija (koraci)
1. Dodati konfigurabilnu opciju:
   - `AppSettings:PerformanceLogging:CaptureSql` (bool)
   - `AppSettings:PerformanceLogging:MaxQueryLength` (int, npr. 2000)
   - `AppSettings:PerformanceLogging:SampleRate` (float 0-1) — opcionalno za smanjenje volumena
2. Instrumentovati centralne helper-e (implementirati u jednom PR-u):
   - `Api/Services/AccessImportService.cs` — u `ExecuteAnalyticsNonQueryAsync` prije `ExecuteNonQueryAsync` dodati:
     ```csharp
     if (_logger.IsEnabled(LogLevel.Debug) && config.CaptureSql) {
       var q = Truncate(sql, maxLen);
       _logger.LogDebug("SQL: {Sql} | Params: {Params} | RequestId: {RequestId}", q, SummarizeParams(cmd.Parameters), requestId);
     }
     ```
   - `Api/Endpoints/SupplierDecisionHubEndpoints.cs` — u `ExecuteSupplierRowsQueryAsync` dodati sličan debug/info log.
   - `Infrastructure/Seed/DatabaseInitializer.cs` — dodati logs za seed/migration SQL.
3. Sažetak parametara i sanitizacija:
   - Nemoj logovati vrednosti koje mogu sadržati PII (npr. `email`, `personal_id`). Umesto vrednosti logovati `paramName=<<value>>` ili napraviti whitelist param imena.
   - Implementirati `SummarizeParams` koja vraća `paramName=(len)` ili hash, i opcionalno sample few values.
4. Truncation i storage:
   - Pre nego što se SQL upiše u log, pozvati `Truncate(sql, maxLen)`.
   - Ako je potrebno kasnije persistiranje u DB, koristiti `TEXT`/`nvarchar(max)` i dodatnu kolonu `QueryTextHash` za brzu deduplikaciju.
5. Correlation:
   - Prolaziti `RequestId` (iz HTTP konteksta ili generisati u `PerformanceLoggingBehavior`) i logovati zajedno sa SQL-om.
6. Feature flag & rollout:
   - Omogućiti slanje samo na `staging` ili `dev` prema configu.
   - Po potrebi uključiti sampling (npr. 10% request-a) kako biste ograničili volumen logova.
7. Tests:
   - Jedinični test za `SummarizeParams`, `Truncate` i da se SQL loguje kada je flag uključen.
   - Integration test koji pokreće tipičan endpoint i proverava da logger poziva očekivane poruke (možete koristiti `ILogger` mock / sink).
8. Monitoring:
   - Nakon enabling-a, pratiti log volumena, veličinu poruka, i performance impact.
   - Dodati alert na rast log volumena i povećanje latencije.

Detaljan plan — centralizovano (duži rad)
1. Add interceptor
   - Implementirati `DbCommandInterceptor` (EF Core) ili `Npgsql` diagnostic listener.
   - Emitovati structured event koji sadrži `CommandText`, `Parameters`, `ElapsedMilliseconds`, `CorrelationId`.
2. Store/attach
   - U `PerformanceLoggingBehavior`, subscribe to diagnostic events for the current request (use `Activity` or `AsyncLocal` correlation) and collect executed commands.
   - Extend `Domain/Model/PerformanceLog` with new fields: `ExecutedCommands` (text JSON array), `CommandsCount`, `TotalDbTimeMs`.
   - Implement EF migration.
3. UI / API
   - Extend `/api/performance` to expose captured commands (with paging/truncation).
4. Safety
   - Sanitize param values (PII), or store only parameter names and counts, or hashed parameter values.
5. Rollout
   - Feature flag, staging rollout, monitor disk and DB growth.

Schema & migration note (if persisting)
- Add to `Domain/Model/PerformanceLog.cs`:
  ```csharp
  public string? ExecutedCommandsJson { get; set; } // JSON array of { text, durationMs, paramsSummary }
  public int CommandsCount { get; set; }
  ```
- Create EF migration and apply to analytics DB. Limit row size and add TTL/purge policy for logs.

Log format recommendation
- Structured JSON-friendly log message or structured logger fields, e.g.:
  - `Event: DbCommandExecuted`
  - `RequestId`, `Handler`, `CommandText` (truncated), `ParamsSummary`, `DurationMs`, `Timestamp`

Privacy & security
- Do not log raw parameter values by default. Use whitelist for safe params or mask values.
- Use sampling and truncation to control sensitive data exposure and storage.

Estimated effort & timeline
- Minimal approach: 1–2 dev days (implement helper logs, config, tests, deploy to staging).
- Centralized interceptor + schema change: 5–10 dev days (implement interceptor, correlation, migration, API surface + QA + staging rollout).

Next steps (suggested)
1. Potvrdite pristup: "minimal" ili "centralized".
2. Ako "minimal": dozvoliću implementaciju u `AccessImportService` + `SupplierDecisionHub` helper-e prvi PR.
3. Nakon PR-a, deploy na staging, enable config, prikupljati realne logove, analizirati.

Reference (lokacije za implementaciju)
- `Api/Services/AccessImportService.cs` — centralni helper-e (ExecuteAnalyticsNonQueryAsync)
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs` — ExecuteSupplierRowsQueryAsync
- `Application/Behaviors/PerformanceLoggingBehavior.cs` — mesto za korelaciju i persisting
- `Domain/Model/PerformanceLog.cs` — schema izmene (ako persistiramo)

---

Ovo je početni plan; spreman sam da odmah implementiram "minimal" pristup i otvorim PR.
