# Architecture

Dixels books rooms, desks and focus pods inside buildings. Admins shape the space (buildings → floors → spaces, space types, opening rules, closures, users); employees find a free space and book it, alone or with guests.

- **Backend**: ABP Framework on .NET 10, a layered monolith, PostgreSQL. `backend/`
- **Frontend**: React 19 + Vite + TypeScript, TanStack Query, shadcn/ui, Tailwind. `frontend/`
- **Shared**: `shared/test-fixtures/`, cases read by both the C# and the TS test suites when one rule is implemented on both sides.

## Backend layers

Each layer may use only the layers above it in this list.

| Project | Holds | Never holds |
|---|---|---|
| `Dixels.Domain.Shared` | Constants (`*Consts`), enums, error codes (`DixelsDomainErrorCodes`), localization texts (`Localization/Dixels/<lang>.json`) | Logic |
| `Dixels.Domain` | Entities (aggregate roots), domain services (`*Manager`, `DomainService`), repository **interfaces**, domain events, background jobs and workers, seed contributors | EF Core, HTTP, DTOs |
| `Dixels.Application.Contracts` | DTOs, app-service interfaces, permission names | Logic |
| `Dixels.Application` | App services: authorize, load, call the domain, map to DTOs (Mapperly) | Business rules (they live in Domain) |
| `Dixels.EntityFrameworkCore` | `DixelsDbContext`, model configuration (`*ModelBuilderExtensions`), custom repository **implementations**, migrations | Business rules |
| `Dixels.HttpApi` | Controllers, HTTP status mapping for error codes | Logic |
| `Dixels.Web` | Host: auth server (OpenIddict), Razor login pages, health, rate limits | Feature code |
| `Dixels.DbMigrator` | Applies migrations and seeds | |

`Volo.Abp.MultiLingualObjects` is ABP source copied in (it isn't on NuGet). Treat it as a library: change it only to fix a bug in it.

## Backend modules

Folders with the same name sit in each layer (`Bookings/`, `SpaceManagement/`, `Users/`, `Emails/`, `Reservations/`).

- **SpaceManagement**: buildings, floors, spaces, space types, constraints (days, hours, durations, capacity), closures/openings (`AvailabilityOverride`). Names are stored per language (`*Translation` rows).
- **Bookings**: bookings and recurring series, guests (colleagues and outsiders), answers (accept / maybe / decline), reminders, guest answer links.
- **Users**: ABP `IdentityUser` with extra properties (e.g. `BuildingId`). There is no separate employee table.
- **Emails**: everything sent by email (templates, .ics invites, the RSVP mailbox reader).
- **Reservations**: the contract between space management and anything that holds rooms for people.

### Boundaries
- **SpaceManagement and Users never reference Bookings.** They announce what happened with local events (`ILocalEventBus`; `…Event` classes in a `…Events.cs` file, `…Handler` listeners), and Bookings reacts. Examples: `SpaceRulesChangedEvent`, `ClosureCreatedEvent`, `SpaceDeletedEvent`, `UserMovedBuildingEvent`; Bookings announces its own (`BookingConfirmedEvent`, `BookingsCancelledEvent`…) for Emails to react to.
- **Previews go through `IReservationImpactProvider`** (`Application/Reservations`): "this change would affect N reservations" is asked in space-management terms, and each holding module (Bookings today; parking or visitors later) answers. A new module plugs in by implementing it, without editing space management.
- **Emails are sent from events, never inline.** Background jobs queue the email in the same transaction as the change ("no booking, no email").

## Frontend

```
src/app/                  router (data router), module registry (modules.ts), route permissions
src/features/<feature>/   api/ components/ hooks/ routes/ + module.tsx (pages, routes, menu, permissions)
src/components/           app-wide pieces and ui/ (shadcn)
src/lib/api/              fetch wrapper (ABP error envelope, 401 → silent renew → retry), query cache, generated API types
src/lib/time/             building-clock date maths; format.ts is the only date formatter
src/i18n/                 texts come from the backend; keys.d.ts is generated
```

- **Modules**: a feature is its folder plus one line in `src/app/modules.ts`. `module.tsx` declares its pages, routes, menu and permissions; `registry.test.tsx` checks they match.
- **Feature boundaries**: a feature may import another feature only through the files listed as public in `scripts/feature-boundaries.mjs` (and every feature's `module`, `permissions` and `queryKeys`). oxlint enforces it, and CI fails if `.oxlintrc.json` drifts from the list.
- **API types**: `src/lib/api/openapi.json` and `schema.d.ts` are generated from the backend's Swagger; `schema.contract.test.ts` checks the hand-written DTO types against them.

## Data
- PostgreSQL in every environment except the EF unit tests (SQLite in memory).
- Times are stored UTC (`timestamptz`); building-local wall-clock times use the building's timezone (`BuildingClock`).
- Profile pictures are files (ABP FileSystem blob provider), not database rows.
- Soft delete (`ISoftDelete`) on buildings, floors, spaces and space types; a cascading delete shares one `DeletionBatchId`.

## Decisions
The reasons behind the shape above are in [`decisions/`](decisions/).
