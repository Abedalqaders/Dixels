# 0005 — Localization: backend texts, names per language

**Status:** accepted (Sep–Oct 2026)

**Context.** Real users read Arabic; the product must grow to about ten languages without frontend work per language.

**Decision.**
- **UI texts** live in ABP backend localization (`Localization/Dixels/<lang>.json`). The frontend loads languages and texts at runtime and has generated key types (`npm run i18n:types`). Adding a language = a `LanguageInfo` + one JSON file.
- **Entity names** (space types, buildings, floors, spaces) use ABP MultiLingualObjects (source copied into `backend/src/Volo.Abp.MultiLingualObjects/`). Every language, English included, is a translation row; the default language's name is required, others fall back to it. Uniqueness: unique index on (Language, NormalizedName) filtered on a copied `IsDeleted` flag; normalized = trimmed, case-insensitive.
- The server picks names by `Accept-Language`. Booking titles, closure reasons and codes aren't translated.
- Western digits in every language; RTL through logical CSS; dates through `lib/time/format`.

**Consequences.** Every new text or error code needs an entry in every language file in the same change. One text box + language dropdown edits names (`LocalizedNameField`).
