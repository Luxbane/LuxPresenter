#pragma once

#include <QString>

namespace Lux {

// Address-bar input -> URL (port of OnlineBrowserView.NavigateTo): empty / "about:blank" / "home" -> empty string
// (home page); text without http(s):// that looks like a host ("a.b", no spaces) -> https://...; anything else ->
// a Google search.
QString normalizeBrowserInput(const QString& input);
bool isHomeUrl(const QString& url);   // "", about:blank, data:text/html...

} // namespace Lux
