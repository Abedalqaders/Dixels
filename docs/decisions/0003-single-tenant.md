# 0003 — Single tenant

**Status:** accepted (Sep 2026)

**Context.** Dixels is installed per organisation; the spec has no organisation id anywhere.

**Decision.** No `IMultiTenant` on Dixels entities. ABP multi-tenancy is switched off and the tenant-management HTTP/Web modules are removed (their tables stay, so no migration was needed).

**Consequences.** One database per customer. Adding tenancy later would touch every entity and query.
