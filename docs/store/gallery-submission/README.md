# CmdPal Gallery submission (staging)

These files are the content of a PR to <https://github.com/microsoft/CmdPal-Extensions>
so the extension appears in Command Palette's built-in Gallery. They are staged here for
review; they are not used by the MSIX build.

Target path in the gallery repo:

```
extensions/everythingpal/everythingpal/
├── extension.json
├── icon.png                  (256x256 PNG)
└── screenshots/
    ├── 01-search-page.png
    ├── 02-home-inline-result.png
    ├── 03-context-menu.png
    └── 04-zero-result.png
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
