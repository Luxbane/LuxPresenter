#pragma once

#include "Models/AppSettings.h"
#include "Models/MediaItem.h"
#include "Models/PresentationItem.h"
#include "Models/Slide.h"
#include "Services/MpvPlayer.h"
#include "Services/PresentationService.h"

#include <QList>
#include <QTimer>
#include <QWidget>

class QLabel;
class QListWidget;
class QPushButton;
class QSlider;
class QSplitter;
class QStackedWidget;

namespace Lux {

class PageView;
class SlideCanvas;
class LiveAlertBannerWidget;

// Preview / Live pane (SlidePaneView.xaml). Header "<Pane> - <item>", then one of three
// pages: slide mode (stanza list over the rendered slide), media-player mode and
// presentation-document mode. Playback engines and projection arrive in later milestones;
// the transport controls are therefore disabled.
class SlidePaneView : public QWidget
{
    Q_OBJECT
public:
    enum class Mode { Slides, Media, PresentationDoc };

    SlidePaneView(const QString& paneTitle, bool isLive, QWidget* parent = nullptr);
    ~SlidePaneView() override;

    QString paneTitle() const { return m_paneTitle; }
    bool isLive() const { return m_isLive; }

    void setMode(Mode mode);
    Mode mode() const;

    void setItemTitle(const QString& title);
    void setItemSubtitle(const QString& subtitle);
    QString headerText() const { return m_headerPlain; }

    // Loads a presentation item into the pane (SlideViewerViewModel.LoadItem): title, subtitle,
    // stanzas and the pane background; selects `slideIndex` (clamped) when there are slides.
    void loadItem(const PresentationItem& item, const QString& background, int slideIndex = 0);
    void setSettings(const AppSettings& settings);
    QString itemTitle() const { return m_itemTitle; }
    QString itemSubtitle() const { return m_itemSubtitle; }
    QList<Slide> slides() const { return m_slides; }

    // ---- presentation document mode (PDF / converted PowerPoint) ----
    using DocumentRef = PresentationService::Document;
    // Shows page `page` (0-based, clamped) of the document; switches the pane to its document page.
    void loadDocument(const DocumentRef& doc, int page);
    void setDocumentPage(int page);                 // clamped; emits documentPageChanged when it changes
    bool hasDocument() const { return m_doc.isValid() && mode() == Mode::PresentationDoc; }
    const DocumentRef& document() const { return m_doc; }
    int documentPage() const { return m_docPage; }
    void clearDocument();
    void loadEmptyPresentation();
    // Document-mode pane with a message instead of a page (document loading or failed to open).
    void showDocumentError(const QString& title, const QString& message);
    PageView* pageView() const { return m_pageView; }
    QPushButton* docPrevButton() const { return m_docPrev; }
    QPushButton* docNextButton() const { return m_docNext; }

    // ---- media mode ----
    void loadMedia(const MediaItem& media);
    void loadEmptyMedia();
    bool hasMedia() const { return mode() == Mode::Media && !m_media.filePath.isEmpty(); }
    MediaItem media() const { return m_media; }

    bool isPlaying() const { return m_isPlaying; }
    void setPlaying(bool play);
    bool isLooping() const { return m_isLooping; }
    void setLooping(bool loop);
    double volume() const { return m_volume; }
    void setVolume(double vol);
    void updateMediaProgress(double positionSec, double durationSec);
    void stopPlayback();

    QPushButton* loopButton() const { return m_loopBtn; }
    QPushButton* playButton() const { return m_playBtn; }
    QSlider* mediaSlider() const { return m_mediaSlider; }
    QSlider* volumeSlider() const { return m_volumeSlider; }

    void setSlides(const QList<Slide>& slides);
    void setCurrentSlideIndex(int index);
    int currentSlideIndex() const;

    QListWidget* stanzaList() const { return m_stanzas; }
    SlideCanvas* canvas() const { return m_canvas; }
    QSplitter* splitter() const { return m_splitter; }
    QStackedWidget* pages() const { return m_pages; }
    QList<QPushButton*> transportButtons() const { return m_transport; }
    QLabel* videoNote() const { return m_videoNote; }
    QLabel* docStatus() const { return m_docStatus; }

signals:
    void currentSlideChanged(int index);   // selection changed (user or programmatic)
    void slideDoubleClicked(int index);
    void documentPageChanged(int page);    // 0-based, user or programmatic
    void mediaPlayPauseRequested(bool play);
    void mediaLoopToggled(bool loop);
    void mediaSeekRequested(double fraction);
    void mediaVolumeChanged(double volume);

private:
    QWidget* buildSlidePage();
    QWidget* buildMediaPage();
    QWidget* buildDocPage();
    void updateHeader();
    void updateDocStatus();
    void updateDocButtons();
    void updateLoopButtonUi();

    QString m_paneTitle;
    bool m_isLive;
    QString m_itemTitle;
    QString m_itemSubtitle;
    QString m_headerPlain;

    QLabel* m_header = nullptr;
    QStackedWidget* m_pages = nullptr;
    QListWidget* m_stanzas = nullptr;
    QSplitter* m_splitter = nullptr;
    LiveAlertBannerWidget* m_alertBanner = nullptr;
    SlideCanvas* m_canvas = nullptr;
    QLabel* m_videoNote = nullptr;
    QLabel* m_docStatus = nullptr;
    QList<QPushButton*> m_transport;
    QList<Slide> m_slides;
    DocumentRef m_doc;
    int m_docPage = 0;
    PageView* m_pageView = nullptr;
    QPushButton *m_docPrev = nullptr, *m_docNext = nullptr;
    MediaItem m_media;

    // Media mode state & controls
    bool m_isPlaying = false;
    bool m_isLooping = false;
    double m_volume = 1.0;
    bool m_isDraggingSlider = false;
    double m_lastDuration = 0.0;

    QWidget* m_videoHost = nullptr;
    QLabel* m_previewPoster = nullptr;
    QSlider* m_mediaSlider = nullptr;
    QLabel* m_timeLabel = nullptr;
    QPushButton* m_loopBtn = nullptr;
    QPushButton* m_rewindBtn = nullptr;
    QPushButton* m_playBtn = nullptr;
    QPushButton* m_forwardBtn = nullptr;
    QSlider* m_volumeSlider = nullptr;

    MpvPlayer m_previewPlayer;
    QTimer m_pollTimer;
};

} // namespace Lux
