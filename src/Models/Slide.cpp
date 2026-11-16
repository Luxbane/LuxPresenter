#include "Models/Slide.h"

namespace Lux {

Slide Slide::clone() const
{
    Slide c = *this;
    c.id = newId();
    return c;
}

QJsonObject Slide::toJson(bool includeMargin) const
{
    QJsonObject o;
    o[QStringLiteral("Id")] = id;
    o[QStringLiteral("Label")] = label;
    o[QStringLiteral("Content")] = content;
    o[QStringLiteral("Title")] = title;
    o[QStringLiteral("Subtitle")] = subtitle;
    o[QStringLiteral("CustomBackgroundPath")] = Json::fromNullable(customBackgroundPath);
    o[QStringLiteral("Type")] = static_cast<int>(type);
    o[QStringLiteral("FontFamily")] = fontFamily;
    o[QStringLiteral("FontSize")] = fontSize;
    o[QStringLiteral("IsBold")] = isBold;
    o[QStringLiteral("IsItalic")] = isItalic;
    o[QStringLiteral("IsUnderline")] = isUnderline;
    o[QStringLiteral("HasShadow")] = hasShadow;
    o[QStringLiteral("HasOutline")] = hasOutline;
    o[QStringLiteral("FontColor")] = fontColor;
    if (includeMargin) {
        QJsonObject m;
        m[QStringLiteral("Left")] = containerMargin.left;
        m[QStringLiteral("Top")] = containerMargin.top;
        m[QStringLiteral("Right")] = containerMargin.right;
        m[QStringLiteral("Bottom")] = containerMargin.bottom;
        o[QStringLiteral("ContainerMargin")] = m;
    }
    return o;
}

Slide Slide::fromJson(const QJsonObject& o)
{
    Slide s;
    s.id = Json::str(o, QStringLiteral("Id"), newId());
    s.label = Json::str(o, QStringLiteral("Label"), s.label);
    s.content = Json::str(o, QStringLiteral("Content"));
    s.title = Json::str(o, QStringLiteral("Title"));
    s.subtitle = Json::str(o, QStringLiteral("Subtitle"));
    s.customBackgroundPath = Json::nullableStr(o, QStringLiteral("CustomBackgroundPath"));
    s.type = Json::enumValue(o, QStringLiteral("Type"), SlideType::Verse, static_cast<int>(SlideType::General));
    s.fontFamily = Json::str(o, QStringLiteral("FontFamily"), s.fontFamily);
    s.fontSize = Json::integer(o, QStringLiteral("FontSize"), s.fontSize);
    s.isBold = Json::boolean(o, QStringLiteral("IsBold"), s.isBold);
    s.isItalic = Json::boolean(o, QStringLiteral("IsItalic"), s.isItalic);
    s.isUnderline = Json::boolean(o, QStringLiteral("IsUnderline"), s.isUnderline);
    s.hasShadow = Json::boolean(o, QStringLiteral("HasShadow"), s.hasShadow);
    s.hasOutline = Json::boolean(o, QStringLiteral("HasOutline"), s.hasOutline);
    s.fontColor = Json::str(o, QStringLiteral("FontColor"), s.fontColor);
    const QJsonValue m = o.value(QStringLiteral("ContainerMargin"));
    if (m.isObject()) {
        const QJsonObject mo = m.toObject();
        s.containerMargin.left = Json::real(mo, QStringLiteral("Left"), s.containerMargin.left);
        s.containerMargin.top = Json::real(mo, QStringLiteral("Top"), s.containerMargin.top);
        s.containerMargin.right = Json::real(mo, QStringLiteral("Right"), s.containerMargin.right);
        s.containerMargin.bottom = Json::real(mo, QStringLiteral("Bottom"), s.containerMargin.bottom);
    }
    return s;
}

} // namespace Lux
