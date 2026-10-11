# Engineering guidelines

How new code is built. [`conventions.md`](conventions.md) covers the mechanics (naming, generated files, commands).

## Use ABP's building blocks
Reach for the ABP way first; deviate only with a reason written in the code comment.

| Need | Use |
|---|---|
| Current time | `IClock` / `Clock` (`DomainService`, `ApplicationService` have it) |
| JSON | `IJsonSerializer` |
| Entity → DTO | `ObjectMapper` with the project's Mapperly mapper classes |
| New id | `GuidGenerator` |
| A business rule broken | `BusinessException(DixelsDomainErrorCodes.X)` with `.WithData(...)` |
| A service resolved lazily | `LazyServiceProvider.LazyGetRequiredService<T>()` |
| Data a repository can't express | Custom repository: interface in Domain, `EfCore…` implementation |
| Per-user data | An `IdentityUser` extra property (mapped to a column when it's filtered on), never a parallel user table |
| Wall-clock DateTime (no zone) | `[DisableDateTimeNormalization]` |
| Cross-module "something happened" | A local event (`ILocalEventBus`) |
| Work after the request | A background job, queued in the same transaction |

## Errors and HTTP status
- A broken rule is a `BusinessException` with a `Dixels:*` code. `DixelsHttpExceptionStatusCodeFinder` turns it into **400** (ABP's default would be 403).
- A conflict with the current state (slot just taken, idempotency key reused) is **409**: map it in `DixelsHttpApiModule.ConfigureHttpStatusCodes` and mirror the mapping in `HttpExceptionStatusCodeTests`.
- Any other status needs a mapping too: a `Dixels:*` code is 400 unless mapped. A real "you may not" is **403** (map the code, as `BookingOrganiserOnly`, or let `[Authorize]` throw); an unknown or deleted item is **404** (`repository.GetAsync` / `EnsureExistsAsync` throw `EntityNotFoundException`; check the parent exists before saving, so it isn't a foreign-key 500). Every mapping is mirrored in `HttpExceptionStatusCodeTests`.
- Every new code gets a text in **every** language file (`en.json`, `ar.json`) in the same change.

## Correctness by default
These are expected in the first version, not added after review:

- **One save, one transaction.** A logical save writes all its fields at once, never one call per field.
- **Optimistic concurrency.** Anything two admins might edit carries a `ConcurrencyStamp`.
- **Races closed in the database.** An app-level check is backed by a unique index, an exclusion constraint or a lock (`SELECT … FOR UPDATE`, `pg_advisory_xact_lock`), because two requests can pass the same check at once.
- **Idempotent creates.** A create that a double-click or retry could repeat takes an idempotency key: stored on the row, unique per user, the first result returned on replay (bookings, series, building/floor/space).
- **Soft-delete-aware uniqueness.** A unique index on a name or other business value of a soft-deleted table is filtered on `IsDeleted = false`, so a deleted item's value can be reused. (Idempotency-key indexes deliberately include deleted rows: a replay after a delete is a 409, see decision 0008.)
- **Retroactive checks.** When a parent rule tightens, the children and bookings it now breaks are found and shown, not only checked on the next write.
- **Scoped cascades.** A cascading delete stamps one batch id, so a restore brings back exactly that batch.
- **Seams for named future needs.** A check that will grow (e.g. per-building admin scoping) goes through one method now.

## Product thinking
Dixels is a real product. When a real, nameable case exists (24-hour buildings, overnight hours, Arabic users), build configuration for it now. Abstract "maybe someday" generality still waits.

## Performance
- Read what a screen needs in a fixed number of queries; no query per row (N+1).
- An admin save that touches many rows uses a bulk statement (`ExecuteUpdateAsync`) when it doesn't need per-entity events.
- A new query on a big table comes with its index; Postgres tests can check the plan (`BookingOverlapQueryPlanTests`).

## Testing
Every change ships with tests in the same PR. Pick the level that can actually catch the bug:

| Level | Project / tool | Use for |
|---|---|---|
| Unit | `Dixels.Domain.Tests` (xUnit + Shouldly + NSubstitute) | Pure domain logic: value objects, validators, calculations |
| Integration (app) | `Dixels.EntityFrameworkCore.Tests` (SQLite in memory) | App services end to end: permissions, rules, DTOs, events, errors. (`Dixels.Application.Tests` only holds the shared base class; app-service tests go here.) |
| Integration (Postgres) | `Dixels.Postgres.Tests` (Testcontainers, `[PostgresFact]`) | Anything SQLite can't prove: SQL, indexes, locks, races, migrations, query plans |
| HTTP | `Dixels.Web.Tests` | Status codes, middleware, host config |
| Frontend | vitest + Testing Library, next to the file (`X.test.tsx`) | Components, hooks, pure functions; every user-visible behaviour change |
| Contract | `tsc` over `ApiDto` types, `enumChecks.ts`, the CI `api-contract` job | Frontend types match the backend |

- A test name states the behaviour in plain words: `A_retry_after_the_floor_was_deleted_is_refused_not_made_again`.
- A rule implemented in both C# and TS reads its cases from one file in `shared/test-fixtures/`.
- A test that passes only after weakening its assertion is a failing test: fix the code.

## Plans
- Split big work into **vertical slices**: each ships backend + frontend + tests and is usable alone. One PR per slice.
- Each slice in a plan states its **trade-off** against the current approach and its **performance cost** (queries, requests, payload, indexes) before asking for approval.
