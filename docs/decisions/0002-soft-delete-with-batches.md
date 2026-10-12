# 0002 — Soft delete with batch cascades

**Status:** accepted (Sep 2026); restore UI removed Oct 2026

**Context.** Deleting a building should be undoable, and ABP's soft delete doesn't cascade to children.

**Decision.**
- Buildings, floors, spaces and space types are soft-deleted (`ISoftDelete`).
- Deleting a building or floor soft-deletes everything under it with one bulk statement per table (`ISpaceHierarchyBulkRepository`), stamping a shared `DeletionBatchId`, so a restore brings back exactly that batch and not children deleted separately earlier.
- Unique indexes on these tables are filtered on `IsDeleted = false`, so a deleted item's name can be reused.
- A space type in use (even by a deleted space) can't be deleted.
- The restore UI was removed (Oct 2026); the restore endpoints stay, so it can come back. Don't re-add it unless asked.

**Consequences.** Every query on these tables relies on ABP's soft-delete filter; code that must see deleted rows disables it explicitly (`IDataFilter.Disable<ISoftDelete>()`). A restore doesn't revive the bookings the delete cancelled.
