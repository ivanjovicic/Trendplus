# Tenant Context Contract

Date: 2026-08-09
Repo: `ivanjovicic/Trendplus`
Owner prompt: `MT01`
Status: application-layer contract only; no request resolution or persistence yet

## Purpose

Define the canonical tenant ownership identity before middleware, EF filters, cache keys or workers invent incompatible shapes.

Supported customer isolation today remains **one deployment/database/storage/cache scope per customer**. This contract prepares shared-SaaS vocabulary; it does not authorize shared-SaaS traffic.

## Canonical identity

| Rule | Contract |
|---|---|
| Type | `Application.Common.Tenancy.TenantId` |
| Shape | immutable GUID/UUID value object |
| Empty/default | invalid; construction throws |
| Canonical string | GUID format `D` via `ToString()` (lowercase hex) |
| Equality | value equality on the underlying GUID |

```csharp
var tenantId = new TenantId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
tenantId.ToString(); // "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
```

## Context semantics

Interface: `Application.Common.Tenancy.ITenantContext`

| State | `IsResolved` | `TenantId` |
|---|---|---|
| Unresolved | `false` | throws `InvalidOperationException` |
| Resolved | `true` | exact `TenantId` |

Fail-closed rules:

- unresolved context must not invent a pilot/default/global tenant;
- unresolved context must not expose `Guid.Empty` as a real tenant;
- no ambient/static global tenant state in Application;
- no DI/runtime registration in `MT01` (added by later prompts).

## What is not TenantId

These identifiers keep their existing business meaning and are **not** ownership authority:

| Identifier | Why it is not tenant ownership |
|---|---|
| `StoreId` / `IDObjekat` | store dimension **inside** a tenant |
| User ID / subject ID | identity subject; membership is separate (`MT02`/`MT03`) |
| Source connection ID | connector resource inside a tenant |
| File path / report ID / batch ID | resource ids that must themselves become tenant-owned later |
| Public `X-Tenant-Id` / query tenant | never authoritative without server-side membership validation |

`TenantId` has no `FromStoreId` / `FromUserId` / `FromHeader` factory. Construction accepts only a non-empty `Guid`.

## Expected later consumers

| Phase | Consumer | Depends on |
|---|---|---|
| `MT02` | trusted request resolver + membership checks | this contract |
| `MT03` | tenant catalog / membership persistence | this contract |
| `MT04`+ | EF entities, filters, unique keys | this contract |
| `MT06` | tenant-qualified cache keys | this contract |
| `MT07` | jobs/outbox/import/connector ownership | this contract |
| `MT08` | documents/reports/exports | this contract |
| `MT10` | shared-SaaS release gate | all prior gates |

## Explicit non-goals of MT01

- no `Program.cs` / middleware changes;
- no authentication/authorization pipeline changes;
- no `TrendplusDbContext` / migrations / entity changes;
- no cache, worker, frontend or deployment changes;
- no production request behavior change.

## Verification

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~TenantContextContractTests
dotnet build Application/Application.csproj --configuration Release
```
