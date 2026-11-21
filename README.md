# LuxPresenter

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Built With: Gemini 3.8 Flash](https://img.shields.io/badge/Built%20With-Gemini%203.8%20Flash-4E75F6.svg?logo=google&logoColor=white)](https://deepmind.google/technologies/gemini/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6.svg)](https://github.com/luxbane/LuxPresenter)
[![itch.io](https://img.shields.io/badge/Download%20on-itch.io-FA5C5C.svg?logo=itchdotio&logoColor=white)](https://luxbane.itch.io/luxpresenter)
[![Ko-fi](https://img.shields.io/badge/Support-Ko--fi-FF5E5B.svg?logo=kofi&logoColor=white)](https://ko-fi.com/luxbane)

A high-performance, modern church presentation and worship projection software built with C# and WPF on .NET 10, developed and crafted with **Gemini 3.8 Flash & GPT 5.6 Luna**.

Designed as an intuitive, fast, and open alternative for church media operators and worship teams.

> 🎮⛪ **Fun Fact:** We officially got a free, full-featured open-source church worship presenter software one day before GTA 6! (Completed on November 18, 2026 — exactly one day before the anticipated November 19 release). *Yes, we got LuxPresenter before GTA VI!*

---

## Key Features

* **Dual Display Architecture:** Clean separation of Operator Control Window and Multi-Display Live Projector Output with smooth blend transitions.
* **Stage Display / Confidence Monitor:** Support for a 3rd stage screen (`Ctrl+Shift+D`) and wireless Web Stage Display (`http://<ip>:8080/stage`) accessible from any browser, smartphone, tablet, or stage TV over Wi-Fi.
* **Song & Hymn Library:** Searchable database with verse/chorus detection, live lyrics formatting, customizable font size, outlines, text shadow, and alignment controls.
* **Open Song Management & Portability:** Native song database management with open JSON backup, import, and export.
* **Presentation Engine (PowerPoint & PDF):** Bundled portable LibreOffice Impress engine combined with Google PDFium and SkiaSharp for slide rendering and smooth multi-screen projection of PPTX, PPT, ODP, and PDF presentations.
* **Integrated Hardware-Accelerated Video Player:** Built-in `libmpv` media playback engine supporting 4K 60fps MP4, MKV, WebM, smooth video looping, and low-latency audio/video rendering.
* **Online Web Integration:** Built-in Microsoft Edge WebView2 incognito browser for real-time online streaming and lyrics search.
* **Quick Overlays & Tickers:** One-click / shortcut toggles for Black screen (`Ctrl+B`), Clear text (`Ctrl+C`), Logo (`F8`), and Nursery/Alert ticker banners.
* **Built-in Auto-Update Checker:** Automatic check for new versions on GitHub Releases with integrated release notes and direct download.

---

## Download & Installation

Pre-compiled binaries and standalone packages are available through the following official channels:

* 🎮 **itch.io (Recommended):** [https://luxbane.itch.io/luxpresenter](https://luxbane.itch.io/luxpresenter)
* 🐙 **GitHub Releases:** [GitHub Releases](https://github.com/luxbane/LuxPresenter/releases)

### Available Packages
* **Installer (`LuxPresenter_Setup.exe`):** Recommended for general use. Installs the complete suite including required media runtimes and LibreOffice presentation engine.
* **Portable Package (`LuxPresenter-Portable.zip`):** Standalone archive that runs out of the box without system-wide installation.
* **Patch Update (`LuxPresenter-Patch.zip`):** Quick updater for existing installations without redownloading heavy runtimes.

---

## Building from Source

### Prerequisites
* Windows 10/11 (64-bit)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
* [Git](https://git-scm.com/) and [Git LFS](https://git-lfs.com/) (required to clone `libmpv-2.dll`)

### Clone and Compile
```bash
# 1. Clone repository with Git LFS
git clone https://github.com/luxbane/LuxPresenter.git
cd LuxPresenter

# 2. Ensure Large Files (libmpv-2.dll) are pulled
git lfs pull

# 3. Build the application
dotnet build -c Release
```

---

## Support & Donate

If you find LuxPresenter helpful for your ministry, church, or organization, consider supporting the continued development:

[![Support on Ko-fi](https://img.shields.io/badge/Support%20on-Ko--fi-FF5E5B?style=for-the-badge&logo=kofi&logoColor=white)](https://ko-fi.com/luxbane)

You can also support the project by leaving a rating or voluntary contribution on [itch.io](https://luxbane.itch.io/luxpresenter). Your support helps cover ongoing maintenance and development of new features!

---

## Open Source Licensing and Attribution

LuxPresenter is an open-source project licensed under the [GNU General Public License v3.0 (GPLv3)](LICENSE).

### Third-Party Software and Runtimes
This software incorporates and interfaces with the following open-source projects:

* **Microsoft Edge WebView2 SDK** — [BSD 2-Clause License](THIRD_PARTY_NOTICES.md#1-microsoft-edge-webview2-sdk)
* **PDFtoImage** — [MIT License](THIRD_PARTY_NOTICES.md#2-pdftoimage)
* **SkiaSharp** — [MIT & BSD-3-Clause Licenses](THIRD_PARTY_NOTICES.md#3-skiasharp--google-skia)
* **Google PDFium** — [Apache License 2.0](THIRD_PARTY_NOTICES.md#4-pdfium-bblanchonpdfiumwin32)
* **mpv Media Player Library (`libmpv`)** — [LGPL v2.1+ / GPL v3+](THIRD_PARTY_NOTICES.md#5-mpv-media-player-library-libmpv)
* **Khronos Vulkan Loader** — [Apache License 2.0](THIRD_PARTY_NOTICES.md#6-khronos-vulkan-loader)
* **LibreOffice Engine** — [Mozilla Public License v2.0 (MPL-2.0)](THIRD_PARTY_NOTICES.md#7-libreoffice-engine-subprocess-presentation-service)
* **Microsoft Direct3D Shader Compiler** — [Microsoft Software License Terms](THIRD_PARTY_NOTICES.md#8-microsoft-direct3d-shader-compiler)

For comprehensive details on licenses, copyright notices, and asset provenance, please consult:
* [`LICENSE`](LICENSE) — Project License
* [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) — Upstream Open-Source Notices
* [`ASSET_LICENSES.md`](ASSET_LICENSES.md) — Visual & Media Asset Documentation
* [`SOFTWARE_BILL_OF_MATERIALS.md`](SOFTWARE_BILL_OF_MATERIALS.md) — Component Inventory

---

## Trademark Notice
All product names, logos, and brands mentioned are property of their respective owners. LuxPresenter is an independent open-source project and is not affiliated with, endorsed by, or sponsored by Softouch Development, Inc. or any other entity.
