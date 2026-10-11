# 0004 — Per-user data on `IdentityUser`

**Status:** accepted (Sep 2026)

**Context.** An `EmployeeBuildingAssignment` table duplicated what ABP's identity user already is.

**Decision.**
- Data about a user is an `IdentityUser` extension property (`DixelsModuleExtensionConfigurator`), mapped to its own column when it's filtered on (e.g. `AbpUsers.BuildingId`). No parallel employee table.
- The Users page lists every identity user, not only the `employee` role.
- `DixelsProfileAppService` replaces ABP's profile service and clears `ExtraProperties` on update: ABP would otherwise let users write any extra property of their own (including `BuildingId`) through `PUT /api/account/my-profile`.

**Consequences.** Reading a mapped extra property needs a tracked query (`AsNoTracking` skips ABP's copy into `ExtraProperties`). A new user-editable property must be whitelisted in `DixelsProfileAppService`, not by removing the `Clear()`. Covered by `MyProfileTests`.
