# 0006 — Profile pictures as files

**Status:** accepted (Oct 2026, PR #23)

**Context.** Profile pictures were first stored in database blob tables.

**Decision.** ABP's FileSystem blob provider; the database provider and its tables (`AbpBlobs`, `AbpBlobContainers`) were removed. The folder is `BlobStoring:FileSystem:BasePath` (dev: `backend/App_Data/blobs`; deploy: the `blobs` volume).

**Consequences.** Backups must include the blob folder. Moving to S3/MinIO later is a provider configuration change, not a code change.
