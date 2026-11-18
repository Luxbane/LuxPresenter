# Asset Provenance and Licensing Documentation

This document records the provenance, licensing, author, and redistribution terms for all fonts, icons, visual artwork, backgrounds, and media assets in LuxPresenter.

---

## 1. Icons and Interface Glyphs
* **Methodology:** Rather than importing heavy proprietary icon packs or third-party bitmap libraries, the application uses **Standard Unicode / System Emojis and Native WPF Vector Geometries**.
* **Visual Symbols Used:**
  * Transport / Actions: `➕`, `➖`, `◀`, `▶`, `▲`, `▼`, `✏️`, `🖥️`, `🌐`, `🖼️`, `⚠️`, `☑`, `🚫`, `🏠`, `🔄`, `✖`, `➔`, `📥`, `📂`, `🎵`, `📽️`.
  * Vector Geometries: WPF standard `Path` and `Rectangle` shapes defined in XAML (`MainWindow.xaml`, `ProjectionWindow.xaml`).
* **Source:** Native Windows System Font Fallback (Segoe UI Emoji / Segoe UI Symbol).
* **License:** Permitted by Windows OS runtime for desktop application UI rendering.
* **Redistribution:** Fully permitted (no proprietary font files bundled; rendered by the client OS).

---

## 2. Application Branding and Logo
* **Files:** `app.ico`, `app_icon.png`
* **Provenance:** Programmatically generated for the project via C# GDI+ drawing script (`CreateNativeFwIcon(256)`).
* **Design:** Dark emerald green rounded rectangle with diagonal sheen and white "FW" glyphs.
* **Trademark Note:** The name "LuxPresenter" and stylized "FW" monogram should be reviewed prior to commercial deployment to prevent consumer confusion with existing trademarks.

---

## 3. Typography and Fonts
* **Referenced System Fonts:**
  * `Tahoma`, `Arial`, `Segoe UI`
  * **Licensing:** Standard Microsoft Windows core system fonts. They are referenced by font family name in XAML markup; no proprietary `.ttf` or `.otf` font files are packaged or redistributed inside LuxPresenter.
* **Bundled LibreOffice Engine Fonts (located inside `Engines/LibreOffice/Fonts`):**
  * **Liberation Fonts** (`LiberationSans`, `LiberationSerif`, `LiberationMono`): SIL Open Font License 1.1 (OFL-1.1).
  * **DejaVu Fonts** (`DejaVuSans`, `DejaVuSerif`): Free Bitstream Vera / DejaVu License.
  * **Noto Fonts** (`NotoSans`, `NotoSerif`): SIL Open Font License 1.1 (OFL-1.1).
  * **Amiri, Alef, Rubik**: SIL Open Font License 1.1 (OFL-1.1).

---

## 4. Bibles and Scripture Texts
* **King James Version (KJV):**
  * **Provenance:** Standard 1611 English translation.
  * **License:** Public Domain in the United States and internationally. (Crown Copyright restrictions apply only within the United Kingdom when printed by authorized publishers).
* **Indonesian Terjemahan Baru (TB):**
  * **Status:** **PROPRIETARY / RESTRICTED.**
  * **Copyright:** © 1974 Lembaga Alkitab Indonesia (LAI).
  * **Compliance Requirement:** Must NOT be bundled in public GitHub releases without documented licensing. Users must import their own scripture database or use open translations (such as TSI or public domain historical texts).

---

## 5. Sample Hymns and Songs
* **Public Domain Hymns (Approved for Redistribution):**
  * *Amazing Grace* (John Newton, 1779) — Public Domain
  * *O Come All Ye Faithful* (C. Frederick Oakeley, 1841) — Public Domain
  * *O Come O Come Emmanuel* (John Mason Neale, 1851) — Public Domain
  * *O Could I Speak The Matchless Worth* (Samuel Medley, 1789) — Public Domain
  * *O For A Faith That Will Not Shrink* (William H. Bathurst, 1831) — Public Domain
* **Copyrighted Worship Songs (REMOVED / UNLICENSED):**
  * *How Great Is Our God* (Chris Tomlin, © 2004 worshiptogether.com songs) — **FAIL: Removed from distribution.**
  * *10,000 Reasons* (Matt Redman, © 2011 Thankyou Music) — **FAIL: Removed from distribution.**

---

## 6. Worship Backgrounds and Media
* **Legacy Proprietary Backgrounds (`CleanBackgrounds`, `autumn_leaves.png`, etc.):**
  * **Status:** **COMPLETELY PURGED.** All 136 carved backgrounds and derived PNGs have been permanently deleted from the source repository.
* **Legacy EasyWorship Database Dumps (`ExtractedEW`, `EW2009_Database`):**
  * **Status:** **COMPLETELY PURGED.** All Paradox database dumps and extraction tools (`inspect.ps1`) have been permanently deleted from the source repository.
* **Default Presentation Backgrounds:**
  * Clean procedural WPF color presets (`#001833` Deep Blue Majesty, `#0B132B` Midnight Navy, `#1A1B41` Royal Indigo, `#1E293B` Slate Charcoal, `#064E3B` Emerald Dark) with full support for user-owned custom background images and videos.
