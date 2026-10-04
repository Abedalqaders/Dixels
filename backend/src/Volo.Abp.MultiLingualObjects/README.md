# Volo.Abp.MultiLingualObjects (vendored)

A verbatim copy of ABP Framework's `framework/src/Volo.Abp.MultiLingualObjects` at tag **10.6.1**
(https://github.com/abpframework/abp/tree/10.6.1/framework/src/Volo.Abp.MultiLingualObjects).
ABP does not publish this package on NuGet; its documentation says to copy the source into the
solution (https://abp.io/docs/10.6/multi-lingual-entities).

- **Don't edit these files.** Keep them identical to ABP's, so this folder can be replaced by an
  official package (or a newer copy) without touching Dixels code. Only the `.csproj` differs:
  it targets this solution's framework and references `Volo.Abp.Localization` from NuGet.
- The namespace stays `Volo.Abp.MultiLingualObjects` for the same reason.
- ABP Framework is licensed under LGPL-3.0 (https://github.com/abpframework/abp/blob/10.6.1/LICENSE.md);
  this copy keeps that license.

Used by Dixels for names people type in several languages (space types, then buildings, floors,
spaces): see `IMultiLingualObjectManager.GetTranslationAsync` / `GetBulkTranslationsAsync`.
