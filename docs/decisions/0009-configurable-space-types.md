# 0009 — Space types are configurable

**Status:** accepted (Sep 2026)

**Context.** The mock had a fixed list (meeting room, focus pod, desk); customers will have others (phone booths, parking later).

**Decision.** `SpaceType` is an admin-managed entity (names per language + an `IconKey` from a fixed set), seeded with the three defaults. A type in use can't be deleted.

**Consequences.** Booking rules don't hang off space types today (they're a flat lookup). A richer model (types carrying policy defaults) was specified and deferred; don't assume it until it's planned.
