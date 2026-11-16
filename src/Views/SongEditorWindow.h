#pragma once

#include "Models/Song.h"

#include <QDialog>

class QCheckBox;
class QComboBox;
class QLabel;
class QLineEdit;
class QPlainTextEdit;
class QPushButton;
class QToolButton;

namespace Lux {

class EditorPreview;

// Song editor (SongEditorWindow.xaml): title, formatting toolbar, lyrics editor with a live
// slide preview of the stanza under the caret, and OK / Cancel.
class SongEditorWindow : public QDialog
{
    Q_OBJECT
public:
    // `existing` == nullptr creates a new song ("New Song" / "[Verse 1]\nEnter lyrics here...").
    explicit SongEditorWindow(const Song* existing = nullptr, QWidget* parent = nullptr);

    // The edited song (valid after the dialog was accepted). Keeps the id and the fields the
    // editor does not expose (author, copyright, key, favourite, colour) of the original.
    Song resultSong() const;
    // "Apply changes to songs in schedule (linked)" checkbox.
    bool applyToSchedule() const;

    // Text shown on the preview slide for the stanza under the caret (SongEditorWindow.UpdatePreview).
    static QString previewTextFor(const QString& lyrics, int caretIndex, const QString& title);

    // --- parts (tests) ---
    QLineEdit* titleEdit() const { return m_title; }
    QPlainTextEdit* lyricsEdit() const { return m_lyrics; }
    QComboBox* fontCombo() const { return m_font; }
    QLineEdit* sizeEdit() const { return m_size; }
    QToolButton* boldButton() const { return m_bold; }
    QToolButton* italicButton() const { return m_italic; }
    QToolButton* underlineButton() const { return m_underline; }
    QToolButton* shadowButton() const { return m_shadow; }
    QToolButton* outlineButton() const { return m_outline; }
    QComboBox* backgroundCombo() const { return m_background; }
    QPushButton* okButton() const { return m_ok; }
    QPushButton* cancelButton() const { return m_cancel; }
    QCheckBox* linkedCheck() const { return m_linked; }
    EditorPreview* preview() const { return m_preview; }
    QString currentBackground() const { return m_currentBackground; }
    QList<QToolButton*> iconButtons() const { return m_iconButtons; }

private slots:
    void updatePreview();
    void resetAttributes();
    void onBackgroundChanged(int index);

private:
    QString selectedFontFamily() const;
    void selectFontFamily(const QString& name);

    Song m_original;
    bool m_isNew = true;
    QString m_currentBackground;

    QLineEdit* m_title;
    QPlainTextEdit* m_lyrics;
    QComboBox* m_font;
    QLineEdit* m_size;
    QToolButton *m_bold, *m_italic, *m_underline, *m_shadow, *m_outline, *m_color;
    QComboBox* m_background;
    EditorPreview* m_preview;
    QCheckBox* m_linked;
    QPushButton *m_ok, *m_cancel;
    QList<QToolButton*> m_iconButtons;
};

// The 16:9 preview panel of the editor (640x360 logical canvas).
class EditorPreview : public QWidget
{
    Q_OBJECT
public:
    explicit EditorPreview(QWidget* parent = nullptr);

    struct State
    {
        QString text;
        QString footer;
        QString background = QStringLiteral("#001833");
        QString fontFamily = QStringLiteral("Tahoma");
        int fontPx = 52;
        bool bold = true, italic = false, outline = true, shadow = true;
    };
    void setState(const State& s);
    const State& state() const { return m_state; }
    QSize sizeHint() const override { return {640, 360}; }

protected:
    void paintEvent(QPaintEvent*) override;

private:
    State m_state;
};

} // namespace Lux
