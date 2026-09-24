# Space management API client

`spaceManagementApi.ts` is a typed fetch wrapper over the backend's auto-generated API
controllers (`Dixels.Application/SpaceManagement/`, exposed via ABP's
`ConventionalControllers.Create(...)` — see `DixelsWebModule.cs`). Route paths mirror the
explicit `[HttpGet]`/`[HttpPut]`/`[HttpPost]` attributes on the app services for the
non-CRUD-shaped methods (`.../tree`, `.../constraints`, `.../resolved-constraints`,
`.../restore`); everything else follows ABP's Get/Create/Update/Delete convention.
