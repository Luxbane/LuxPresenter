#pragma once

#include "Models/AppSettings.h"
#include "Models/Slide.h"

#include <QString>
#include <optional>

namespace Lux {

// Port of ProjectionState (Services/IProjectionService.cs): everything the projector output needs.
struct ProjectionState
{
    std::optional<Slide> activeSlide;
    QString activeBackground = QStringLiteral("#001833");
    bool isLive = true;
    bool isBlack = false;
    bool isClear = false;
    bool isLogo = false;
    QString logoPath;

    // Standalone video (Media tab) - the engine supports it; the Media tab arrives with Milestone 6.
    bool isVideoMode = false;
    QString mediaFilePath;
    bool isVideoPlaying = false;
    double videoVolume = 1.0;
    bool isVideoLooping = true;

    // Presentation document page (PDF / converted PowerPoint) shown instead of slide text (M5)
    bool isPresentationMode = false;
    QString presentationPdfPath;
    int presentationPage = 0;          // 0-based

    // Formatting & transitions ("Blend" or "Cut")
    QString songTextAlignment = QStringLiteral("Center");
    QString songVerticalAlignment = QStringLiteral("Center");
    QString songTransition = QStringLiteral("Blend");
    QString slideTransition = QStringLiteral("Blend");
    QString mediaTransition = QStringLiteral("Blend");
    QString screenTransition = QStringLiteral("Blend");
    int transitionDurationMs = 500;
    bool showVerseLabels = false;
    QString labelLocation = QStringLiteral("Top Center");
    QString labelSize = QStringLiteral("Normal");
    bool indentLabel = false;
    bool autoWrapWords = true;

    // Word formatting
    bool capitalizeAllWords = true;
    bool capitalizeFirstCharEachLine = false;
    bool autoCapitalizeKeywords = false;
    QString capitalizeKeywords;
    QString songFontFamily = QStringLiteral("Arial, Tahoma");
    bool songFontBold = true;
    int songFontSize = 13;
    int songFontHeightPercent = 14;

    // ProjectionService.UpdateSettings
    void applySettings(const AppSettings& s)
    {
        songTextAlignment = s.songTextAlignment;
        songVerticalAlignment = s.songVerticalAlignment;
        songTransition = s.songTransition;
        slideTransition = s.slideTransition;
        mediaTransition = s.mediaTransition;
        screenTransition = s.screenTransition;
        transitionDurationMs = s.transitionDurationMs;
        showVerseLabels = s.showVerseLabels;
        labelLocation = s.labelLocation;
        labelSize = s.labelSize;
        indentLabel = s.indentLabel;
        autoWrapWords = s.autoWrapWords;
        capitalizeAllWords = s.capitalizeAllWords;
        capitalizeFirstCharEachLine = s.capitalizeFirstCharEachLine;
        autoCapitalizeKeywords = s.autoCapitalizeKeywords;
        capitalizeKeywords = s.capitalizeKeywords;
        songFontFamily = s.songFontFamily;
        songFontBold = s.songFontBold;
        songFontSize = s.songFontSize;
        songFontHeightPercent = s.songFontHeightPercent;
    }
};

} // namespace Lux
