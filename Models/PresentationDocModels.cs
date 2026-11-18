using System;
using System.Collections.Generic;

namespace LuxPresenter.Models;

public class PresentationDocItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileExtension { get; set; } = ".pptx";
    public int SlideCount { get; set; } = 0;
    public string ThumbnailPath { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; } = DateTime.Now;
}

public class PresentationDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public double CanvasWidth { get; set; } = 1920;
    public double CanvasHeight { get; set; } = 1080;
    public List<PresentationSlide> Slides { get; set; } = new();
}

public class PresentationSlide
{
    public int Index { get; set; } // 1-based
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string? ThumbnailPath { get; set; }
    public string? ImagePreviewPath { get; set; }

    // UI state indicators
    public bool IsSelectedInPreview { get; set; } = false;
    public bool IsCurrentlyLive { get; set; } = false;
}
