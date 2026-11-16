#include "Models/Song.h"

#include "Services/LyricsFormatter.h"

#include <QFile>
#include <QRegularExpression>

#include <algorithm>

namespace Lux {

namespace {

struct Block
{
    QString content;
    QString sepAfter;
};

bool isHeaderLine(const QString& trimmedFirstLine)
{
    return (trimmedFirstLine.startsWith(QLatin1Char('[')) && trimmedFirstLine.endsWith(QLatin1Char(']')))
        || (trimmedFirstLine.endsWith(QLatin1Char(':')) && !trimmedFirstLine.contains(QLatin1Char(' ')));
}

QString trimChars(QString s, QChar a, QChar b)
{
    while (!s.isEmpty() && (s.front() == a || s.front() == b))
        s.remove(0, 1);
    while (!s.isEmpty() && (s.back() == a || s.back() == b))
        s.chop(1);
    return s;
}

} // namespace

PresentationItem Song::toPresentationItem(const AppSettings* settings) const
{
    const AppSettings defaults;
    const AppSettings& eff = settings ? *settings : defaults;

    QString bg;
    if (!defaultBackground.trimmed().isEmpty()
        && (defaultBackground.startsWith(QLatin1Char('#')) || QFile::exists(defaultBackground)))
        bg = defaultBackground;

    PresentationItem item;
    item.title = title;
    item.subtitle = author.trimmed().isEmpty() ? QString(QStringLiteral("")) : QStringLiteral("By %1").arg(author);
    item.category = ItemCategory::Song;
    item.backgroundPath = bg;

    int blankBefore = 0; // blank lines between the previous block and the one being built
    auto makeSlide = [&](const QString& label, const QString& content, SlideType type, Thickness margin) {
        Slide s;
        s.label = label;
        s.title = title;
        s.content = content;
        s.type = type;
        s.customBackgroundPath = bg;
        s.containerMargin = margin;
        s.fontFamily = !eff.songFontFamily.trimmed().isEmpty() ? eff.songFontFamily
                     : (!fontFamily.trimmed().isEmpty() ? fontFamily : QStringLiteral("Arial"));
        s.fontSize = eff.songFontSize > 0 ? eff.songFontSize : (fontSize > 0 ? fontSize : 13);
        s.isBold = eff.songFontBold || isBold;
        s.isItalic = isItalic;
        s.isUnderline = isUnderline;
        s.hasShadow = hasShadow;
        s.hasOutline = hasOutline;
        s.fontColor = fontColor;
        s.blankLinesBefore = blankBefore;
        return s;
    };
    const Thickness normalMargin{1, 1, 1, 2};
    const Thickness stanzaMargin{1, 1, 1, 20};

    if (lyricsRaw.trimmed().isEmpty()) {
        item.slides.append(makeSlide(QStringLiteral("Slide 1"),
                                     LyricsFormatter::format(title, &eff), SlideType::Verse, normalMargin));
        return item;
    }

    QString normalized = lyricsRaw;
    normalized.replace(QStringLiteral("\r\n"), QStringLiteral("\n")).replace(QLatin1Char('\r'), QLatin1Char('\n'));
    normalized = normalized.trimmed();

    // Split into blocks on runs of 2+ newlines, remembering each separator.
    QList<Block> blocks;
    {
        static const QRegularExpression sepRe(QStringLiteral("\\n{2,}"));
        qsizetype pos = 0;
        auto it = sepRe.globalMatch(normalized);
        auto addBlock = [&](const QString& raw, const QString& sep) {
            const QString c = raw.trimmed();
            if (!c.isEmpty())
                blocks.append({c, sep});
        };
        while (it.hasNext()) {
            const auto m = it.next();
            addBlock(normalized.mid(pos, m.capturedStart() - pos), m.captured());
            pos = m.capturedEnd();
        }
        addBlock(normalized.mid(pos), QString());
    }

    for (qsizetype b = 0; b < blocks.size(); ++b) {
        const QString& block = blocks.at(b).content;
        // "\n\n" = one blank line, "\n\n\n" = two, ...: the separator is kept as spacing information.
        blankBefore = b == 0 ? 0 : std::max<qsizetype>(1, blocks.at(b - 1).sepAfter.count(QLatin1Char('\n')) - 1);
        QString label;
        QString content = block;
        SlideType type = SlideType::Verse;

        const QStringList lines = block.split(QLatin1Char('\n'), Qt::KeepEmptyParts);
        const QString first = lines.value(0).trimmed();
        auto rest = [&]() { return lines.mid(1).join(QLatin1Char('\n')).trimmed(); };

        if (lines.size() > 1 && first.startsWith(QLatin1Char('[')) && first.endsWith(QLatin1Char(']'))) {
            label = trimChars(first, QLatin1Char('['), QLatin1Char(']'));
            content = rest();
            if (label.contains(QStringLiteral("Chorus"), Qt::CaseInsensitive)) type = SlideType::Chorus;
            else if (label.contains(QStringLiteral("Bridge"), Qt::CaseInsensitive)) type = SlideType::Bridge;
            else if (label.contains(QStringLiteral("Ending"), Qt::CaseInsensitive)) type = SlideType::Ending;
        } else if (!lines.isEmpty() && first.endsWith(QLatin1Char(':')) && !lines.at(0).contains(QLatin1Char(' '))) {
            label = first;
            label.chop(1);
            while (label.endsWith(QLatin1Char(':')))
                label.chop(1);
            content = rest();
            if (label.contains(QStringLiteral("Chorus"), Qt::CaseInsensitive)) type = SlideType::Chorus;
            else if (label.contains(QStringLiteral("Bridge"), Qt::CaseInsensitive)) type = SlideType::Bridge;
        } else {
            label = QStringLiteral(""); // unlabeled continuation block
        }

        bool isStanzaEnd = false;
        if (b + 1 < blocks.size()) {
            const qsizetype nlCount = blocks.at(b).sepAfter.count(QLatin1Char('\n'));
            if (nlCount >= 3) {
                isStanzaEnd = true;
            } else {
                const QString nextFirst = blocks.at(b + 1).content.split(QLatin1Char('\n')).value(0).trimmed();
                isStanzaEnd = isHeaderLine(nextFirst);
            }
        }

        item.slides.append(makeSlide(label, LyricsFormatter::format(content, &eff), type,
                                     isStanzaEnd ? stanzaMargin : normalMargin));
    }

    if (item.slides.isEmpty())
        item.slides.append(makeSlide(QStringLiteral("Slide 1"),
                                     LyricsFormatter::format(title, &eff), SlideType::Verse, normalMargin));
    return item;
}

QJsonObject Song::toJson() const
{
    QJsonObject o;
    o[QStringLiteral("Id")] = id;
    o[QStringLiteral("Title")] = title;
    o[QStringLiteral("Author")] = author;
    o[QStringLiteral("Copyright")] = copyright;
    o[QStringLiteral("Key")] = key;
    o[QStringLiteral("IsFavorite")] = isFavorite;
    o[QStringLiteral("LyricsRaw")] = lyricsRaw;
    o[QStringLiteral("DefaultBackground")] = Json::fromNullable(defaultBackground);
    o[QStringLiteral("FontFamily")] = fontFamily;
    o[QStringLiteral("FontSize")] = fontSize;
    o[QStringLiteral("IsBold")] = isBold;
    o[QStringLiteral("IsItalic")] = isItalic;
    o[QStringLiteral("IsUnderline")] = isUnderline;
    o[QStringLiteral("HasShadow")] = hasShadow;
    o[QStringLiteral("HasOutline")] = hasOutline;
    o[QStringLiteral("FontColor")] = fontColor;
    return o;
}

Song Song::fromJson(const QJsonObject& o)
{
    Song s;
    s.id = Json::str(o, QStringLiteral("Id"), newId());
    s.title = Json::str(o, QStringLiteral("Title"));
    s.author = Json::str(o, QStringLiteral("Author"));
    s.copyright = Json::str(o, QStringLiteral("Copyright"));
    s.key = Json::str(o, QStringLiteral("Key"));
    s.isFavorite = Json::boolean(o, QStringLiteral("IsFavorite"), false);
    s.lyricsRaw = Json::str(o, QStringLiteral("LyricsRaw"));
    s.defaultBackground = Json::nullableStr(o, QStringLiteral("DefaultBackground"));
    s.fontFamily = Json::str(o, QStringLiteral("FontFamily"), s.fontFamily);
    s.fontSize = Json::integer(o, QStringLiteral("FontSize"), s.fontSize);
    s.isBold = Json::boolean(o, QStringLiteral("IsBold"), s.isBold);
    s.isItalic = Json::boolean(o, QStringLiteral("IsItalic"), s.isItalic);
    s.isUnderline = Json::boolean(o, QStringLiteral("IsUnderline"), s.isUnderline);
    s.hasShadow = Json::boolean(o, QStringLiteral("HasShadow"), s.hasShadow);
    s.hasOutline = Json::boolean(o, QStringLiteral("HasOutline"), s.hasOutline);
    s.fontColor = Json::str(o, QStringLiteral("FontColor"), s.fontColor);
    return s;
}

} // namespace Lux
