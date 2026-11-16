#include "Services/LyricsFormatter.h"

#include <QRegularExpression>
#include <QStringList>

namespace Lux {

QString LyricsFormatter::format(const QString& raw, const AppSettings* settings)
{
    if (raw.isEmpty())
        return QString();
    if (!settings)
        return raw;

    QString text = raw;

    // 1. Capitalize all words (ALL CAPS)
    if (settings->capitalizeAllWords)
        return text.toUpper();

    // 2. Capitalize the first letter on each line
    if (settings->capitalizeFirstCharEachLine) {
        QStringList lines = text.split(QLatin1Char('\n'), Qt::KeepEmptyParts);
        for (QString& line : lines) {
            for (qsizetype j = 0; j < line.size(); ++j) {
                if (line.at(j).isLetter()) {
                    line[j] = line.at(j).toUpper();
                    break;
                }
            }
        }
        text = lines.join(QLatin1Char('\n'));
    }

    // 3. Capitalize the first character of keywords
    if (settings->autoCapitalizeKeywords && !settings->capitalizeKeywords.trimmed().isEmpty()) {
        const QStringList parts = settings->capitalizeKeywords.split(
            QRegularExpression(QStringLiteral("[\\r\\n,;]")), Qt::SkipEmptyParts);
        for (const QString& part : parts) {
            const QString kw = part.trimmed();
            if (kw.isEmpty())
                continue;
            const QRegularExpression re(
                QStringLiteral("\\b%1\\b").arg(QRegularExpression::escape(kw)),
                QRegularExpression::CaseInsensitiveOption | QRegularExpression::UseUnicodePropertiesOption);
            QString out;
            qsizetype last = 0;
            auto it = re.globalMatch(text);
            while (it.hasNext()) {
                const QRegularExpressionMatch m = it.next();
                out += text.mid(last, m.capturedStart() - last);
                QString word = m.captured();
                if (!word.isEmpty())
                    word[0] = word.at(0).toUpper();
                out += word;
                last = m.capturedEnd();
            }
            out += text.mid(last);
            text = out;
        }
    }
    return text;
}

} // namespace Lux
