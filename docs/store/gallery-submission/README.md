# CmdPal Gallery submission (staging)

These files are the content of a PR to <https://github.com/microsoft/CmdPal-Extensions>
so the extension appears in Command Palette's built-in Gallery. They are staged here for
review; they are not used by the MSIX build.

> 2026-10-21 / 1.0.1.0: `extension.json` now mentions smart matching (case/whitespace-insensitive
> single-word queries, `term1` finds `Term 1`). The PNGs in this folder are still the old
> 1366×768 copies — replace them with the active v2 1920×1080 set from
> `docs/store/screenshots/` (including the optional `05-program-files.png`) before opening the PR;
> only paths are updated in docs, the image files themselves are QA/Lead-owned.

Target path in the gallery repo:

```
extensions/everythingpal/everythingpal/
├── extension.json
├── icon.png                  (256x256 PNG)
└── screenshots/
    ├── 01-search-page.png
    ├── 02-home-inline-result.png
    ├── 03-context-menu.png
    ├── 04-zero-result.png
    └── 05-program-files.png   (optional; v2 1920×1080)
```

Install source: Microsoft Store product ID `9NFD76MCDRJZ`
(`https://apps.microsoft.com/detail/9NFD76MCDRJZ`).

Submission steps (per `docs/CONTRIBUTING.md` in that repo):

1. Fork `microsoft/CmdPal-Extensions`.
2. Create `extensions/everythingpal/everythingpal/` and copy these files in
   (screenshots go in a `screenshots/` subfolder; drop `$schema` or keep the relative path valid
   for that location — it is `../../../.github/schemas/extension.schema.json` from the extension dir).
3. `git checkout -b add-everythingpal`, commit, push to your fork.
4. Open a PR against `main`; CI validates the schema, id/folder match, and icon size.
5. After maintainer merge + gallery regeneration, the extension appears in CmdPal Settings →
   Extensions → Gallery.


## Status

PR opened: https://github.com/microsoft/CmdPal-Extensions/pull/207 — waiting for Microsoft CLA signature (@microsoft-github-policy-service agree) and maintainer review.

