# 0007 — What rule changes and deletes do to bookings

**Status:** accepted (Sep 2026)

**Context.** Without it, admins changed rules blind, and employees silently kept bookings that no longer fit, or lost them without knowing.

**Decision.**
- **Rule changes** (constraints at any level, capacity) and **new closures**: before saving, the admin sees the upcoming confirmed bookings the new rules would reject (the same validator a booking runs, lasting rules only). They choose **Keep them** (default; the booking keeps its rules snapshot) or **Cancel them** (admin cancel with a reason, same transaction).
- **Deleting** a building, floor or space always cancels its upcoming bookings; the dialog shows the count first.
- **Moving or unassigning an employee** always cancels their upcoming bookings in the old building.
- Employees see admin-cancelled bookings struck through with the reason until the time passes; their own cancels just disappear. The owner is emailed about an admin cancel.

**Consequences.** One impact check and one admin-cancel path serve constraint saves, closures and deletes (see 0001 for how they're wired).
