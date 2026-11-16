#pragma once

// Small helpers for reading/writing the JSON files shared with the reference
// application (System.Text.Json, PascalCase keys, enums stored as integers).

#include <QJsonArray>
#include <QJsonObject>
#include <QJsonValue>
#include <QString>
#include <QUuid>

namespace Lux {

inline QString newId()
{
    return QUuid::createUuid().toString(QUuid::WithoutBraces);
}

namespace Json {

inline QString str(const QJsonObject& o, const QString& key, const QString& def = QString())
{
    const QJsonValue v = o.value(key);
    return v.isString() ? v.toString() : def;
}

// Nullable string: absent/null -> null QString, "" -> empty (non-null) QString.
inline QString nullableStr(const QJsonObject& o, const QString& key)
{
    const QJsonValue v = o.value(key);
    if (!v.isString())
        return QString();
    const QString s = v.toString();
    return s.isNull() ? QStringLiteral("") : s;
}

inline QJsonValue fromNullable(const QString& s)
{
    return s.isNull() ? QJsonValue(QJsonValue::Null) : QJsonValue(s);
}

inline bool boolean(const QJsonObject& o, const QString& key, bool def)
{
    const QJsonValue v = o.value(key);
    return v.isBool() ? v.toBool() : def;
}

inline int integer(const QJsonObject& o, const QString& key, int def)
{
    const QJsonValue v = o.value(key);
    return v.isDouble() ? v.toInt(def) : def;
}

inline double real(const QJsonObject& o, const QString& key, double def)
{
    const QJsonValue v = o.value(key);
    return v.isDouble() ? v.toDouble(def) : def;
}

inline QStringList stringList(const QJsonObject& o, const QString& key)
{
    QStringList out;
    const QJsonValue v = o.value(key);
    if (v.isArray()) {
        const QJsonArray a = v.toArray();
        for (const QJsonValue& e : a)
            if (e.isString())
                out << e.toString();
    }
    return out;
}

inline QJsonArray fromStringList(const QStringList& list)
{
    QJsonArray a;
    for (const QString& s : list)
        a.append(s);
    return a;
}

// Reads an enum stored as an integer; falls back to `def` when out of range.
template <class E>
E enumValue(const QJsonObject& o, const QString& key, E def, int maxValue)
{
    const int v = integer(o, key, static_cast<int>(def));
    return (v >= 0 && v <= maxValue) ? static_cast<E>(v) : def;
}

} // namespace Json
} // namespace Lux
