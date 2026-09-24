# Space management API client

No backend endpoints exist yet for this feature — `Dixels.Application` only has a
`Properties/` folder, `Dixels.Application.Contracts` has no Building/Floor/Space DTOs,
and `Dixels.HttpApi` only exposes a placeholder controller. This folder is reserved for
the HTTP client that will call those endpoints once they exist.

Do not add fake/mocked API calls here — the current pages use static mock data directly
(see `../routes/AdminBuildingsPage.tsx`), which is the honest state until the backend
Application/HttpApi layers are built out.
