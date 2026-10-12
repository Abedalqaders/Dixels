# 0001 — Modules talk through events and contracts

**Status:** accepted (Oct 2026, PRs #28–#34)

**Context.** Space management, users and bookings used to call each other directly: deleting a room called booking code, saving rules ran booking checks. Every new module (parking, visitors) would have meant editing space management.

**Decision.**
- A module announces what happened with an ABP local event (`ILocalEventBus`): `SpaceRulesChangedEvent`, `ClosureCreatedEvent`, `SpaceDeletedEvent`, `UserMovedBuildingEvent`, `BookingConfirmedEvent`… Listeners are `…Handler` classes in the module that cares.
- A preview ("this would affect N reservations") goes through `IReservationImpactProvider`, which each reservation-holding module implements.
- SpaceManagement and Users reference nothing in Bookings (one known exception, listed in `docs/architecture.md`).

**Consequences.** A new module plugs in without editing existing services. The flow of one action is spread over a publisher and its handlers, so read the `…Events.cs` file and search for handlers when tracing behaviour. Local events run in the same transaction, so a failing handler rolls the whole save back.
