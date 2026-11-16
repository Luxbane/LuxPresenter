# Importing a Bible translation (JSON)

LuxPresenter ships with one Bible: the King James Version (public domain, see `licenses/KJV-NOTICE.txt`).
Other translations (for example *Alkitab Terjemahan Baru*) are copyrighted and are **not** included. You can import
one that you are licensed to use from a JSON file: **Scriptures tab > Import > Import Translation...** or
**Scriptures menu > Import Bible Translation (.json)...**. `sample-bible.json` (also under Import > Save Sample File...)
is a small file in this format with made-up demo text.

## Format (schema 1)

```json
{
  "schema": 1,
  "id": "TB",
  "name": "Terjemahan Baru",
  "language": "id",
  "copyright": "Who owns it / licence note (shown as a tooltip on the version tab)",
  "books": [
    {
      "id": 1,
      "name": "Kejadian",
      "shortName": "Kej",
      "chapters": [
        ["verse 1 text", "verse 2 text", "verse 3 text"],
        ["chapter 2, verse 1", null, "chapter 2, verse 3"]
      ]
    }
  ]
}
```

| Field | Rule |
|---|---|
| `schema` | optional, must be `1` |
| `id` | required, 2-12 characters: letters, digits, `_`, `-`. Not case sensitive; must be unique (the id `KJV` is taken). |
| `name` | required, 1-80 characters, must be unique |
| `language`, `copyright` | optional text |
| `books[].id` | whole number 1-66 in the usual order (1 Genesis ... 39 Malachi, 40 Matthew ... 66 Revelation); each book once |
| `books[].name` | required; shown in the book list, and accepted when typing a reference |
| `books[].shortName` | optional abbreviation, also accepted in the search box |
| `books[].chapters` | array of chapters; a chapter is an array of verse texts (first entry = verse 1) |
| verse text | a string. `null` or `""` means "this verse does not exist in this translation" (numbering gaps) |

Partial translations are fine (for example only the New Testament); chapter and verse counts come from the data.
Leading/trailing blank space in verses is removed.

The flat list used by the original program (`[{"BookId":1,"Book":"Genesis","Chapter":1,"Verse":1,"Text":"...","Version":"TB"}, ...]`,
one version per file) is accepted too and converted.

## What happens on import

* The file is validated completely first. Problems are listed with their position, e.g.
  `books[2].chapters[0][4]: expected a string or null.`; nothing is installed unless the file is valid.
* An `id` or `name` that is already installed (including the built-in KJV) is refused. An existing translation is never overwritten.
* The translation is stored (in the documented format) as `Data\Bibles\<ID>.json` next to the program's data, not inside the
  program files. It appears as a new tab in the Scriptures tab, is remembered across restarts and works with
  navigation, search, Preview, Go Live and the schedule.
* **Import > Remove Current Translation...** deletes the installed copy (never your original file). The KJV cannot be removed.
