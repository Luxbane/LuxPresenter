#include "Services/BackgroundUtil.h"
#include <QRegularExpression>
#include <QDir>
#include <QFile>
#include <QFileInfo>
namespace Lux {
namespace Backgrounds {
const char* const kDefaultColor = "#001833";
QStringList imageExtensions()
{
    return {QStringLiteral("jpg"), QStringLiteral("jpeg"), QStringLiteral("jfif"), QStringLiteral("png"), QStringLiteral("bmp"),
            QStringLiteral("gif"), QStringLiteral("ico"), QStringLiteral("svg")};
}
QStringList videoExtensions()
{
    return {QStringLiteral("mp4"), QStringLiteral("m4v"), QStringLiteral("mov"), QStringLiteral("mkv"), QStringLiteral("avi"),
            QStringLiteral("wmv"), QStringLiteral("flv"), QStringLiteral("webm"), QStringLiteral("mpg"), QStringLiteral("mpeg"),
            QStringLiteral("ts"), QStringLiteral("mts"), QStringLiteral("m2ts")};
}
bool isVideoPath(const QString& path)
{
    return !path.trimmed().isEmpty() && videoExtensions().contains(QFileInfo(path).suffix().toLower());
}
bool isImagePath(const QString& path)
{
    return !path.trimmed().isEmpty() && imageExtensions().contains(QFileInfo(path).suffix().toLower());
}
bool isColor(const QString& value)
{
    const QString v = value.trimmed();
    if (!v.startsWith(QLatin1Char('#')))
        return false;
    static const QRegularExpression re(QStringLiteral("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$"));
    return re.match(v).hasMatch();
}
QString fileDialogFilter()
{
    auto pat = [](const QStringList& e) {
        QStringList l;
        for (const QString& x : e)
            l << QStringLiteral("*.") + x;
        return l.join(QLatin1Char(' '));
    };
    return QStringLiteral("Backgrounds (%1 %2);;Images (%1);;Videos (%2);;All Files (*.*)")
        .arg(pat(imageExtensions()), pat(videoExtensions()));
}
bool itemFromFile(const QString& path, BackgroundItem* out, QString* error)
{
    const QFileInfo fi(path);
    if (!fi.exists() || !fi.isFile()) {
        if (error)
            *error = QStringLiteral("%1: the file does not exist.").arg(fi.fileName());
        return false;
    }
    BackgroundItem b;
    if (isVideoPath(path))
        b.type = BackgroundType::Video;
    else if (isImagePath(path))
        b.type = BackgroundType::Image;
    else {
        if (error)
            *error = QStringLiteral("%1: unsupported file type (images: %2; videos: %3).")
                         .arg(fi.fileName(), imageExtensions().join(QStringLiteral(", ")), videoExtensions().join(QStringLiteral(", ")));
        return false;
    }
    b.name = fi.completeBaseName();
    b.filePath = QDir::toNativeSeparators(fi.absoluteFilePath());
    b.hexColor = QString();
    if (out)
        *out = b;
    return true;
}
BackgroundItem itemFromColor(const QString& hex, const QString& name)
{
    BackgroundItem b;
    b.type = BackgroundType::Color;
    b.hexColor = hex.trimmed().toUpper();
    b.name = name.isEmpty() ? b.hexColor : name;
    b.filePath = QString();
    return b;
}
QList<BackgroundItem> builtinColors()
{
    struct C { const char* id; const char* name; const char* hex; };
    static const C list[] = {{"builtin-navy", "Navy (default)", "#001833"}, {"builtin-black", "Black", "#000000"},
                             {"builtin-midnight", "Midnight Blue", "#0B132B"}, {"builtin-forest", "Forest Green", "#0B3D2E"},
                             {"builtin-wine", "Wine", "#4A0E0E"}, {"builtin-charcoal", "Charcoal", "#222222"},
                             {"builtin-plum", "Plum", "#2E1A47"}, {"builtin-slate", "Slate", "#1E293B"}};
    QList<BackgroundItem> out;
    for (const C& c : list) {
        BackgroundItem b;
        b.id = QString::fromLatin1(c.id);
        b.name = QString::fromLatin1(c.name);
        b.type = BackgroundType::Color;
        b.hexColor = QString::fromLatin1(c.hex);
        b.isDefault = b.id == QLatin1String("builtin-navy");
        out << b;
    }
    return out;
}
QString usable(const QString& background, const QString& fallback)
{
    const QString v = background.trimmed();
    if (isColor(v))
        return v;
    if (!v.isEmpty() && QFileInfo(v).isFile() && (isImagePath(v) || isVideoPath(v)))
        return v;
    return fallback;
}
QString valueOf(const BackgroundItem& item)
{
    return item.type == BackgroundType::Color ? item.hexColor : item.filePath;
}
} // namespace Backgrounds
} // namespace Lux

