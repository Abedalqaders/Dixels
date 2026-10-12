# 0008 — Idempotency keys in the database

**Status:** accepted (Oct 2026, PR #78)

**Context.** A double-click, a network blip or a retry after a lost answer can send a create twice. A duplicate booking blocks a room for others; a duplicate floor confuses admins.

**Decision.**
- Creates that a retry could repeat take a client-made key (one UUID per form opening): bookings, booking series, and admin adds of building / floor / space.
- The key is a column on the created row, with a unique index per user. A replay with the same details returns the first result; the same key with different details, or after the result was cancelled or deleted, is a 409.
- Concurrent copies are serialized by a database lock (the space row for bookings, `pg_advisory_xact_lock` on the key for admin adds); the unique index is the backstop.
- **Not** in a cache (Redis): the key must commit in the same transaction as the row, survive restarts and expire never. A cache would add a distributed lock and reconciliation for no measurable speed gain (the lookup is one indexed query).

**Not applied** (judged unnecessary): cancelling (repeating it changes nothing), emails (a rare duplicate is harmless), bulk cancels and deletes (already skip what's done).
