#include "Views/SongEditorWindow.h"

#include "Services/TextRepair.h"
#include "Services/ThemeService.h"
#include "Views/Components/TextRender.h"
#include "Views/UiUtil.h"

#include <QApplication>
#include <QCheckBox>
#include <QComboBox>
#include <QFile>
#include <QFileInfo>
#include <QHBoxLayout>
#include <QIcon>
#include <QLabel>
#include <QLineEdit>
#include <QPainter>
#include <QPixmap>
#include <QPlainTextEdit>
#include <QPushButton>
#include <QSplitter>
#include <QTextCursor>
#include <QToolButton>
#include <QVBoxLayout>

#include <algorithm>

namespace Lux {

namespace {

struct Preset { const char* name; const char* hex; };
const Preset kBackgrounds[] = {
    {"Deep Blue Majesty", "#001833"}, {"Midnight Navy", "#0B132B"}, {"Royal Indigo", "#1A1B41"},
    {"Slate Charcoal", "#1E293B"},    {"Emerald Dark", "#064E3B"},
};

struct FontChoice { const char* label; const char* family; };
const FontChoice kFonts[] = {
    {"Default (Tahoma)", "Tahoma"}, {"Arial", "Arial"},       {"Segoe UI", "Segoe UI"},
    {"Times New Roman", "Times New Roman"}, {"Trebuchet MS", "Trebuchet MS"}, {"Verdana", "Verdana"},
};

QToolButton* iconButton(const QString& glyph, const QString& tip, bool emoji, QWidget* parent)
{
    auto* b = new QToolButton(parent);
    b->setText(glyph);
    b->setFont(emoji ? segoeEmoji(12) : segoeSymbol(12));
    b->setToolTip(tip);
    b->setFixedSize(22, 22);
    b->setAutoRaise(true);
    return b;
}

QToolButton* toggleButton(const QString& text, const QString& tip, bool checked, QWidget* parent)
{
    auto* b = new QToolButton(parent);
    b->setText(text);
    b->setToolTip(tip);
    b->setCheckable(true);
    b->setChecked(checked);
    b->setFixedSize(22, 22);
    b->setStyleSheet(QStringLiteral(
        "QToolButton{background:palette(button); border:1px solid #CBD5E1; color:palette(window-text); font-size:11px;}"
        "QToolButton:checked{background:#BFDBFE; border:1px solid #3B82F6; color:#1E3A8A;}"
        "QToolButton:disabled{color:#94A3B8;}"));
    return b;
}

QString stripSectionTag(const QString& block)
{
    const QStringList lines = block.split(QLatin1Char('\n'));
    if (lines.size() > 1 && lines[0].trimmed().startsWith(QLatin1Char('[')) && lines[0].trimmed().endsWith(QLatin1Char(']')))
        return lines.mid(1).join(QLatin1Char('\n')).trimmed();
    if (!lines.isEmpty() && lines[0].trimmed().endsWith(QLatin1Char(':')) && !lines[0].contains(QLatin1Char(' ')))
        return lines.mid(1).join(QLatin1Char('\n')).trimmed();
    return block;
}

} // namespace

// ---------------------------------------------------------------------------------------
// EditorPreview
// ---------------------------------------------------------------------------------------

EditorPreview::EditorPreview(QWidget* parent) : QWidget(parent)
{
    setMinimumSize(200, 112);
    setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);
}

void EditorPreview::setState(const State& s)
{
    m_state = s;
    update();
}

void EditorPreview::paintEvent(QPaintEvent*)
{
    constexpr qreal kW = 640, kH = 360;
    QPainter p(this);
    p.setRenderHint(QPainter::Antialiasing);
    p.fillRect(rect(), QColor(0x88, 0x88, 0x88));
    const qreal scale = std::min(width() / kW, height() / kH);
    p.translate((width() - kW * scale) / 2, (height() - kH * scale) / 2);
    p.scale(scale, scale);
    p.setClipRect(QRectF(0, 0, kW, kH));
    p.fillRect(QRectF(0, 0, kW, kH), Qt::black);

    // Background: colour or image file (relative paths resolve against the app directory)
    QColor bgColor(0x00, 0x18, 0x33);
    const QString bg = m_state.background;
    bool drewImage = false;
    if (bg.startsWith(QLatin1Char('#')) && QColor::isValidColorName(bg)) {
        bgColor = QColor(bg);
    } else if (!bg.trimmed().isEmpty()) {
        QString path = bg;
        if (QFileInfo(path).isRelative())
            path = QCoreApplication::applicationDirPath() + QLatin1Char('/') + bg;
        const QPixmap pm(path);
        if (!pm.isNull()) {
            p.drawPixmap(QRectF(0, 0, kW, kH), pm, pm.rect());
            drewImage = true;
        }
    }
    if (!drewImage)
        p.fillRect(QRectF(0, 0, kW, kH), bgColor);

    // Layout: margin 32,20; text row (*) + footer row (auto, 13px text + 8px top margin)
    const QRectF area(32, 20, kW - 64, kH - 40);
    const qreal footerH = 13 * 1.25 + 8;
    const QRectF row(area.left(), area.top(), area.width(), area.height() - footerH);

    if (!m_state.text.isEmpty()) {
        QFont f(m_state.fontFamily);
        f.setPixelSize(std::max(1, m_state.fontPx));
        f.setBold(m_state.bold);
        f.setItalic(m_state.italic);
        TextBlockStyle st;
        st.font = f;
        st.width = 1720; // Viewbox measures its child unconstrained (infinite width => 1720)
        st.strokeThickness = 2.5;
        st.outline = m_state.outline;
        st.shadow = m_state.shadow;
        const TextBlockMetrics m = measureTextBlock(m_state.text, st);
        const qreal bw = m.width + st.strokeThickness * 2, bh = m.height + st.strokeThickness * 2;
        st.width = bw; // centre inside the measured box
        const qreal s = std::min({1.0, row.width() / bw, row.height() / bh});
        p.save();
        p.translate(row.center().x() - bw * s / 2, row.center().y() - bh * s / 2);
        p.scale(s, s);
        drawTextBlock(&p, m_state.text, st, QPointF(0, 0));
        p.restore();
    }

    if (!m_state.footer.isEmpty()) {
        p.setFont(tahoma(13));
        const QRectF fr(area.left(), area.bottom() - footerH + 8, area.width(), footerH - 8);
        p.setPen(QColor(0, 0, 0, 230));
        p.drawText(fr.translated(1, 1), Qt::AlignLeft | Qt::AlignVCenter, m_state.footer);
        p.setPen(QColor(0xCB, 0xD5, 0xE1));
        p.drawText(fr, Qt::AlignLeft | Qt::AlignVCenter, m_state.footer);
    }
}

// ---------------------------------------------------------------------------------------
// SongEditorWindow
// ---------------------------------------------------------------------------------------

SongEditorWindow::SongEditorWindow(const Song* existing, QWidget* parent) : QDialog(parent)
{
    setWindowTitle(QStringLiteral("Editor"));
    setWindowIcon(QIcon(QStringLiteral(":/app_icon.png")));
    resize(820, 580);
    setSizeGripEnabled(true);

    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(8, 8, 8, 8);
    root->setSpacing(0);

    const QString barStyle = QStringLiteral("QFrame#EditorBar{background:palette(alternate-base); border:1px solid palette(mid);}");

    // 1. Title bar + quick icons
    auto* titleBar = new QFrame(this);
    titleBar->setObjectName(QStringLiteral("EditorBar"));
    titleBar->setStyleSheet(barStyle);
    auto* tl = new QHBoxLayout(titleBar);
    tl->setContentsMargins(6, 4, 6, 4);
    auto* titleLabel = new QLabel(QStringLiteral("Title"), titleBar);
    titleLabel->setFont(tahoma(11, true));
    m_title = new QLineEdit(titleBar);
    m_title->setFixedSize(320, 22);
    tl->addWidget(titleLabel);
    tl->addSpacing(8);
    tl->addWidget(m_title);
    tl->addStretch(1);

    auto* info = iconButton(QStringLiteral("\u2139"), QStringLiteral("Song Information"), false, titleBar);
    auto* print = iconButton(QStringLiteral("\U0001F5A8"), QStringLiteral("Print"), true, titleBar);
    auto* cut = iconButton(QStringLiteral("\u2702"), QStringLiteral("Cut"), false, titleBar);
    auto* copy = iconButton(QStringLiteral("\U0001F4CB"), QStringLiteral("Copy"), true, titleBar);
    auto* paste = iconButton(QStringLiteral("\U0001F4C4"), QStringLiteral("Paste"), true, titleBar);
    auto* undo = iconButton(QStringLiteral("\u21A9"), QStringLiteral("Undo"), false, titleBar);
    auto* redo = iconButton(QStringLiteral("\u21AA"), QStringLiteral("Redo"), false, titleBar);
    auto* zoom = iconButton(QStringLiteral("\U0001F50D"), QStringLiteral("Zoom"), true, titleBar);
    m_iconButtons = {info, print, cut, copy, paste, undo, redo, zoom};
    for (QToolButton* b : {info, print, zoom}) { // the reference buttons have no behaviour
        b->setEnabled(false);
        b->setToolTip(b->toolTip() + QStringLiteral(" - not available"));
    }
    auto addSep = [&] {
        auto* s = new QFrame(titleBar);
        s->setFrameShape(QFrame::VLine);
        s->setStyleSheet(QStringLiteral("color:#CBD5E1;"));
        s->setFixedHeight(16);
        tl->addSpacing(2);
        tl->addWidget(s);
        tl->addSpacing(2);
    };
    tl->addWidget(info);
    tl->addWidget(print);
    addSep();
    tl->addWidget(cut);
    tl->addWidget(copy);
    tl->addWidget(paste);
    addSep();
    tl->addWidget(undo);
    tl->addWidget(redo);
    addSep();
    tl->addWidget(zoom);
    root->addWidget(titleBar);
    root->addSpacing(4);

    // 2. Formatting toolbar
    auto* fmt = new QFrame(this);
    fmt->setObjectName(QStringLiteral("EditorBar"));
    fmt->setStyleSheet(barStyle);
    auto* fl = new QHBoxLayout(fmt);
    fl->setContentsMargins(6, 3, 6, 3);
    fl->setSpacing(1);
    m_font = new QComboBox(fmt);
    m_font->setFixedSize(140, 22);
    for (const FontChoice& f : kFonts)
        m_font->addItem(QString::fromLatin1(f.label), QString::fromLatin1(f.family));
    m_size = new QLineEdit(QStringLiteral("13"), fmt);
    m_size->setFixedSize(30, 22);
    m_size->setAlignment(Qt::AlignCenter);
    m_bold = toggleButton(QStringLiteral("B"), QStringLiteral("Bold"), true, fmt);
    QFont bf = m_bold->font(); bf.setBold(true); m_bold->setFont(bf);
    m_italic = toggleButton(QStringLiteral("I"), QStringLiteral("Italic"), false, fmt);
    QFont itf = m_italic->font(); itf.setItalic(true); m_italic->setFont(itf);
    m_underline = toggleButton(QStringLiteral("U"), QStringLiteral("Underline"), false, fmt);
    QFont uf = m_underline->font(); uf.setUnderline(true); m_underline->setFont(uf);
    m_shadow = toggleButton(QStringLiteral("S"), QStringLiteral("Shadow"), true, fmt);
    m_outline = toggleButton(QStringLiteral("O"), QStringLiteral("Outline Text"), true, fmt);
    QFont of = m_outline->font(); of.setBold(true); m_outline->setFont(of);
    m_color = toggleButton(QStringLiteral("A"), QStringLiteral("Font Color - not available"), false, fmt);
    m_color->setCheckable(false);
    m_color->setEnabled(false);
    QFont cf = m_color->font(); cf.setBold(true); m_color->setFont(cf);
    m_background = new QComboBox(fmt);
    m_background->setFixedSize(145, 22);
    for (const Preset& pr : kBackgrounds)
        m_background->addItem(QString::fromLatin1(pr.name), QString::fromLatin1(pr.hex));
    auto* reset = new QPushButton(QStringLiteral("Reset Attributes"), fmt);
    reset->setFlat(true);
    reset->setCursor(Qt::PointingHandCursor);
    reset->setStyleSheet(QStringLiteral("QPushButton{background:transparent; border:none; color:#64748B; font-size:10px; padding:0 4px;}"));
    fl->addWidget(m_font);
    fl->addSpacing(3);
    fl->addWidget(m_size);
    fl->addSpacing(5);
    for (QToolButton* b : {m_bold, m_italic, m_underline, m_shadow, m_outline, m_color}) {
        fl->addWidget(b);
        fl->addSpacing(1);
    }
    fl->addSpacing(8);
    fl->addWidget(m_background);
    fl->addStretch(1);
    fl->addWidget(reset);
    root->addWidget(fmt);
    root->addSpacing(6);

    // 3. Work area: lyrics (1.1*) | preview (1*)
    auto* split = new QSplitter(Qt::Horizontal, this);
    split->setHandleWidth(6);
    split->setStyleSheet(QStringLiteral("QSplitter::handle{background:#CBD5E1;}"));
    split->setChildrenCollapsible(false);
    m_lyrics = new QPlainTextEdit(split);
    m_lyrics->setFont(tahoma(11));
    m_lyrics->setFrameShape(QFrame::NoFrame);
    m_lyrics->setStyleSheet(QStringLiteral("QPlainTextEdit{padding:8px; border:1px solid palette(mid); background:palette(base); color:palette(text);}"));
    m_lyrics->setTabChangesFocus(false);
    auto* previewFrame = new QFrame(split);
    previewFrame->setStyleSheet(QStringLiteral("QFrame#PreviewFrame{background:#888888; border:1px solid #B2BAC2;}"));
    previewFrame->setObjectName(QStringLiteral("PreviewFrame"));
    auto* pl = new QVBoxLayout(previewFrame);
    pl->setContentsMargins(10, 10, 10, 10);
    m_preview = new EditorPreview(previewFrame);
    pl->addWidget(m_preview);
    split->addWidget(m_lyrics);
    split->addWidget(previewFrame);
    split->setStretchFactor(0, 11);
    split->setStretchFactor(1, 10);
    split->setSizes({396, 360});
    root->addWidget(split, 1);

    // 4. Bottom: linked checkbox + OK / Cancel
    auto* bottom = new QHBoxLayout;
    bottom->setContentsMargins(0, 8, 0, 0);
    m_linked = new QCheckBox(QStringLiteral("Apply changes to songs in schedule (linked)"), this);
    m_linked->setChecked(true);
    m_linked->setFont(tahoma(11));
    m_ok = new QPushButton(QStringLiteral("OK"), this);
    m_ok->setFixedSize(75, 24);
    m_ok->setDefault(true);
    QFont okf = m_ok->font(); okf.setBold(true); m_ok->setFont(okf);
    m_cancel = new QPushButton(QStringLiteral("Cancel"), this);
    m_cancel->setFixedSize(75, 24);
    bottom->addWidget(m_linked);
    bottom->addStretch(1);
    bottom->addWidget(m_ok);
    bottom->addSpacing(6);
    bottom->addWidget(m_cancel);
    root->addLayout(bottom);

    // --- initial state ---
    m_isNew = (existing == nullptr);
    if (existing) {
        m_original = *existing;
        // Legacy Windows-1252 control characters are shown as the punctuation they stand for; the stored
        // text is kept untouched unless the user edits it (see resultSong()).
        m_title->setText(displayText(existing->title));
        m_lyrics->setPlainText(displayText(existing->lyricsRaw));
        m_currentBackground = existing->defaultBackground.isNull() ? QStringLiteral("") : existing->defaultBackground;
        selectFontFamily(existing->fontFamily);
        m_size->setText(QString::number(existing->fontSize > 0 ? existing->fontSize : 13));
        m_bold->setChecked(existing->isBold);
        m_italic->setChecked(existing->isItalic);
        m_underline->setChecked(existing->isUnderline);
        m_shadow->setChecked(existing->hasShadow);
        m_outline->setChecked(existing->hasOutline);
    } else {
        m_original = Song();
        m_title->setText(QStringLiteral("New Song"));
        m_lyrics->setPlainText(QStringLiteral("[Verse 1]\nEnter lyrics here..."));
        m_currentBackground = QStringLiteral("#001833"); // the combo starts on "Deep Blue Majesty"
        selectFontFamily(QStringLiteral("Tahoma"));
        m_size->setText(QStringLiteral("13"));
    }
    // Show the matching preset without altering the song's own background value.
    {
        const int idx = m_background->findData(m_currentBackground, Qt::UserRole, Qt::MatchFixedString);
        QSignalBlocker block(m_background);
        m_background->setCurrentIndex(idx >= 0 ? idx : 0);
    }

    // --- connections ---
    connect(m_title, &QLineEdit::textChanged, this, &SongEditorWindow::updatePreview);
    connect(m_lyrics, &QPlainTextEdit::textChanged, this, &SongEditorWindow::updatePreview);
    connect(m_lyrics, &QPlainTextEdit::cursorPositionChanged, this, &SongEditorWindow::updatePreview);
    connect(m_font, &QComboBox::currentIndexChanged, this, &SongEditorWindow::updatePreview);
    connect(m_size, &QLineEdit::textChanged, this, &SongEditorWindow::updatePreview);
    for (QToolButton* b : {m_bold, m_italic, m_underline, m_shadow, m_outline})
        connect(b, &QToolButton::clicked, this, &SongEditorWindow::updatePreview);
    connect(m_background, &QComboBox::currentIndexChanged, this, &SongEditorWindow::onBackgroundChanged);
    connect(reset, &QPushButton::clicked, this, &SongEditorWindow::resetAttributes);
    connect(m_ok, &QPushButton::clicked, this, &QDialog::accept);
    connect(m_cancel, &QPushButton::clicked, this, &QDialog::reject);

    // Edit actions on the lyrics box
    connect(cut, &QToolButton::clicked, m_lyrics, &QPlainTextEdit::cut);
    connect(copy, &QToolButton::clicked, m_lyrics, &QPlainTextEdit::copy);
    connect(paste, &QToolButton::clicked, m_lyrics, &QPlainTextEdit::paste);
    connect(undo, &QToolButton::clicked, m_lyrics, &QPlainTextEdit::undo);
    connect(redo, &QToolButton::clicked, m_lyrics, &QPlainTextEdit::redo);
    cut->setEnabled(false);
    copy->setEnabled(false);
    undo->setEnabled(false);
    redo->setEnabled(false);
    connect(m_lyrics, &QPlainTextEdit::copyAvailable, cut, &QToolButton::setEnabled);
    connect(m_lyrics, &QPlainTextEdit::copyAvailable, copy, &QToolButton::setEnabled);
    connect(m_lyrics, &QPlainTextEdit::undoAvailable, undo, &QToolButton::setEnabled);
    connect(m_lyrics, &QPlainTextEdit::redoAvailable, redo, &QToolButton::setEnabled);

    updatePreview();
}

void SongEditorWindow::selectFontFamily(const QString& name)
{
    const QString wanted = name.trimmed().isEmpty() ? QStringLiteral("Tahoma") : name;
    for (int i = 0; i < m_font->count(); ++i) {
        const QString tag = m_font->itemData(i).toString();
        if (tag.compare(wanted, Qt::CaseInsensitive) == 0 || m_font->itemText(i).contains(wanted, Qt::CaseInsensitive)) {
            m_font->setCurrentIndex(i);
            return;
        }
    }
    m_font->setCurrentIndex(0);
}

QString SongEditorWindow::selectedFontFamily() const
{
    const QString tag = m_font->currentData().toString();
    return tag.isEmpty() ? QStringLiteral("Tahoma") : tag;
}

void SongEditorWindow::resetAttributes()
{
    selectFontFamily(QStringLiteral("Tahoma"));
    m_size->setText(QStringLiteral("13"));
    m_bold->setChecked(true);
    m_italic->setChecked(false);
    m_underline->setChecked(false);
    m_shadow->setChecked(true);
    m_outline->setChecked(true);
    updatePreview();
}

void SongEditorWindow::onBackgroundChanged(int index)
{
    if (index < 0)
        return;
    m_currentBackground = m_background->itemData(index).toString();
    updatePreview();
}

QString SongEditorWindow::previewTextFor(const QString& lyrics, int caretIndex, const QString& title)
{
    if (lyrics.trimmed().isEmpty())
        return title;
    QString normalized = lyrics;
    normalized.replace(QStringLiteral("\r\n"), QStringLiteral("\n")).replace(QLatin1Char('\r'), QLatin1Char('\n'));
    const int caret = std::clamp(caretIndex, 0, static_cast<int>(normalized.size()));

    int start = 0;
    const int from = std::max(0, caret - 1);
    const int prevBreak = from >= 1 ? static_cast<int>(normalized.lastIndexOf(QStringLiteral("\n\n"), from - 1)) : -1;
    if (prevBreak >= 0)
        start = prevBreak + 2;
    int end = static_cast<int>(normalized.size());
    const int nextBreak = static_cast<int>(normalized.indexOf(QStringLiteral("\n\n"), caret));
    if (nextBreak >= 0)
        end = nextBreak;

    QString block = start < end ? normalized.mid(start, end - start).trimmed() : QString();
    if (block.trimmed().isEmpty()) {
        int lineStart = static_cast<int>(normalized.lastIndexOf(QLatin1Char('\n'), from));
        lineStart = lineStart >= 0 ? lineStart + 1 : 0;
        int lineEnd = static_cast<int>(normalized.indexOf(QLatin1Char('\n'), caret));
        if (lineEnd < 0)
            lineEnd = static_cast<int>(normalized.size());
        block = normalized.mid(lineStart, lineEnd - lineStart).trimmed();
    }
    if (block.trimmed().isEmpty())
        block = title;
    return stripSectionTag(block);
}

void SongEditorWindow::updatePreview()
{
    if (!m_preview || !m_lyrics || !m_title)
        return;
    bool ok = false;
    int fsize = m_size->text().toInt(&ok);
    if (!ok || fsize <= 0)
        fsize = 13;

    EditorPreview::State s;
    s.fontFamily = selectedFontFamily();
    s.fontPx = static_cast<int>(fsize * 4.0);
    s.bold = m_bold->isChecked();
    s.italic = m_italic->isChecked();
    s.outline = m_outline->isChecked();
    s.shadow = m_shadow->isChecked();
    s.background = m_currentBackground.trimmed().isEmpty() ? QStringLiteral("#001833") : m_currentBackground;

    const QString raw = m_lyrics->toPlainText();
    if (raw.trimmed().isEmpty()) {
        s.text = m_title->text();
        s.footer = m_title->text();
    } else {
        s.text = previewTextFor(raw, m_lyrics->textCursor().position(), m_title->text());
        s.footer = QStringLiteral("%1 (Preview)").arg(m_title->text());
    }
    m_preview->setState(s);
}

Song SongEditorWindow::resultSong() const
{
    Song r = m_original;
    const QString shownTitle = m_title->text();
    const QString shownLyrics = m_lyrics->toPlainText();
    auto unixNewlines = [](QString s) { return s.replace(QStringLiteral("\r\n"), QStringLiteral("\n")).replace(QLatin1Char('\r'), QLatin1Char('\n')); };
    // Unchanged fields keep their stored text byte for byte, even if it contains legacy characters.
    r.title = (!m_isNew && shownTitle == displayText(m_original.title)) ? m_original.title.trimmed() : shownTitle.trimmed();
    r.lyricsRaw = (!m_isNew && unixNewlines(shownLyrics) == unixNewlines(displayText(m_original.lyricsRaw)))
                      ? m_original.lyricsRaw
                      : shownLyrics;
    r.defaultBackground = m_currentBackground;
    r.fontFamily = selectedFontFamily();
    bool ok = false;
    const int fsize = m_size->text().toInt(&ok);
    r.fontSize = (ok && fsize > 0) ? fsize : 13;
    r.isBold = m_bold->isChecked();
    r.isItalic = m_italic->isChecked();
    r.isUnderline = m_underline->isChecked();
    r.hasShadow = m_shadow->isChecked();
    r.hasOutline = m_outline->isChecked();
    return r;
}

bool SongEditorWindow::applyToSchedule() const
{
    return m_linked->isChecked();
}

} // namespace Lux
