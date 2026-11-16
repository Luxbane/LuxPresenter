# Third-Party Software Licenses & Notices

LuxPresenter makes use of the following open-source software libraries, components, and datasets. This document provides attribution and the complete licensing notices required by each project.

---

## Table of Contents
1. [Overview](#overview)
2. [Qt Framework (LGPL-3.0)](#1-qt-framework)
3. [libmpv Video Engine (LGPL-2.1+)](#2-libmpv-video-engine)
4. [Google PDFium (BSD-3-Clause / Apache-2.0)](#3-google-pdfium)
5. [Microsoft Edge WebView2 SDK & Runtime (BSD-3-Clause)](#4-microsoft-edge-webview2-sdk--runtime)
6. [uBlock Origin Extension (GPL-3.0)](#5-ublock-origin-extension)
7. [LibreOffice Integration (MPL-2.0)](#6-libreoffice-integration)
8. [King James Version Bible (Public Domain)](#7-king-james-version-bible)

---

## Overview

| Component | Upstream Project / Author | License | Usage in LuxPresenter |
|---|---|---|---|
| **Qt 6.12** | [The Qt Company](https://www.qt.io/) | LGPL-3.0 | Application framework, GUI widgets, network & SVG |
| **libmpv** | [mpv-player team](https://github.com/mpv-player/mpv) | LGPL-2.1+ | Hardware-accelerated background video & audio |
| **PDFium** | [Google / Chromium](https://pdfium.googlesource.com/pdfium/) | BSD-3-Clause | Native PDF presentation page rendering |
| **WebView2 SDK** | [Microsoft Corporation](https://developer.microsoft.com/microsoft-edge/webview2/) | BSD-3-Clause | Embedded browser runtime integration |
| **uBlock Origin** | [Raymond Hill (gorhill)](https://github.com/gorhill/uBlock) | GPL-3.0 | Content filtering & ad blocker extension for WebView2 |
| **LibreOffice** | [The Document Foundation](https://www.documentfoundation.org/) | MPL-2.0 | Background headless PPTX/ODP to PDF conversion |
| **KJV Bible** | [Project Gutenberg #10](https://www.gutenberg.org/ebooks/10) | Public Domain | Default offline Scripture database |

---

## 1. Qt Framework

* **License:** GNU Lesser General Public License version 3 (LGPL-3.0)
* **Copyright:** (C) 2026 The Qt Company Ltd. and other contributors.
* **Website:** https://www.qt.io/ / https://www.gnu.org/licenses/lgpl-3.0.html

LuxPresenter links to the Qt 6 libraries dynamically (`Qt6Core.dll`, `Qt6Gui.dll`, `Qt6Widgets.dll`, `Qt6Network.dll`, `Qt6Svg.dll`). The Qt libraries themselves are not modified. Under the terms of the LGPLv3:
- Users are entitled to inspect, modify, and relink the Qt libraries with alternative binary versions.
- Complete corresponding source code for the Qt libraries is available from upstream at https://download.qt.io/official_releases/qt/.

```
GNU LESSER GENERAL PUBLIC LICENSE
Version 3, 29 June 2007

Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
Everyone is permitted to copy and distribute verbatim copies
of this license document, but changing it is not allowed.

This version of the GNU Lesser General Public License incorporates
the terms and conditions of version 3 of the GNU General Public
License, supplemented by the additional permissions listed below.
...
```

---

## 2. libmpv Video Engine

* **License:** GNU Lesser General Public License version 2.1 or later (LGPL-2.1+)
* **Copyright:** (C) mpv developers / zhongfly mpv-winbuild
* **Upstream:** https://github.com/mpv-player/mpv
* **Builder:** https://github.com/zhongfly/mpv-winbuild (unmodified LGPL build, `-Dgpl=false`)

LuxPresenter loads `libmpv-2.dll` dynamically at runtime using `LoadLibrary`. The library is not modified. Users can replace `libmpv-2.dll` with another binary build of the same API version.

```
GNU LESSER GENERAL PUBLIC LICENSE
Version 2.1, February 1999

Copyright (C) 1991, 1999 Free Software Foundation, Inc.
51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
Everyone is permitted to copy and distribute verbatim copies
of this license document, but changing it is not allowed.
```

---

## 3. Google PDFium

* **License:** BSD-3-Clause / Apache-2.0
* **Copyright:** (C) 2014-2026 Google Inc. / Benoit Blanchon (pdfium-binaries)
* **Upstream:** https://pdfium.googlesource.com/pdfium / https://github.com/bblanchon/pdfium-binaries

```
Copyright 2014-2026 Benoit Blanchon
Copyright 2014 PDFium Authors. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * Neither the name of Google Inc. nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

PDFium binaries statically include third-party subcomponents under their respective open-source licenses:
- **FreeType:** The FreeType Project License / GPLv2
- **HarfBuzz:** MIT License
- **libpng:** PNG Reference Library License version 2
- **zlib:** zlib License (Jean-loup Gailly and Mark Adler)
- **libjpeg-turbo:** IJG License / Modified BSD
- **ICU:** Unicode License / ICU License
- **Little CMS (lcms):** MIT License
- **Abseil:** Apache License 2.0
- **Highway:** Apache License 2.0 / BSD-3-Clause
- **FastFloat / SimdUTF:** Apache License 2.0 / MIT License

---

## 4. Microsoft Edge WebView2 SDK & Runtime

* **License:** BSD-3-Clause / Microsoft Edge WebView2 Runtime License
* **Copyright:** (C) Microsoft Corporation. All rights reserved.
* **Website:** https://developer.microsoft.com/microsoft-edge/webview2/

```
Copyright (C) Microsoft Corporation. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * The name of Microsoft Corporation, or the names of its contributors 
may not be used to endorse or promote products derived from this
software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED.
```

---

## 5. uBlock Origin Extension

* **License:** GNU General Public License version 3 (GPL-3.0)
* **Author:** Raymond Hill (gorhill) and contributors
* **Repository:** https://github.com/gorhill/uBlock

LuxPresenter bundles the unmodified uBlock Origin WebExtension to provide fast, efficient, and reliable content filtering, anti-tracking, and advertisement blocking inside the embedded WebView2 browser tab. Complete source code is available at the upstream repository above.

---

## 6. LibreOffice Integration

* **License:** Mozilla Public License version 2.0 (MPL-2.0)
* **Publisher:** The Document Foundation (https://www.documentfoundation.org/)
* **Usage:** LuxPresenter invokes `soffice.exe --headless --convert-to pdf` as an external standalone process to convert PowerPoint presentations.

LuxPresenter does not link against LibreOffice code or modify its executables. LibreOffice and The Document Foundation are trademarks of their respective owners. Complete source code is available at https://www.libreoffice.org/download/source-code/.

---

## 7. King James Version Bible

* **Status:** Public Domain (except in the UK where Crown copyright applies to commercial printing)
* **Source:** Project Gutenberg eBook #10 (https://www.gutenberg.org/ebooks/10)
* **Bundled verses:** 31,102 verses across 66 books in `Data/bibles.json`.
* **Note:** No copyrighted Bible translations are bundled. Users can import their own translations (e.g. Indonesian Terjemahan Baru / TB) via `File > Scriptures > Import Bible Translation`.
