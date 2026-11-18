using System;
using System.Collections.Generic;

namespace LuxPresenter.Models;

public class ScheduleFileData
{
    public string Version { get; set; } = "1.0";
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public List<ScheduleItemData> Items { get; set; } = new();
}

public class ScheduleItemData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = "Song";
    public ItemCategory Category { get; set; } = ItemCategory.Song;
    public PresentationData Presentation { get; set; } = new();
}

public class PresentationData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public ItemCategory Category { get; set; } = ItemCategory.Song;
    public string? BackgroundPath { get; set; }
    public List<SlideData> Slides { get; set; } = new();
}

public class SlideData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = "Slide";
    public string Content { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string? CustomBackgroundPath { get; set; }
    public SlideType Type { get; set; } = SlideType.Verse;

    public string FontFamily { get; set; } = "Tahoma";
    public int FontSize { get; set; } = 13;
    public bool IsBold { get; set; } = true;
    public bool IsItalic { get; set; } = false;
    public bool IsUnderline { get; set; } = false;
    public bool HasShadow { get; set; } = true;
    public bool HasOutline { get; set; } = true;
    public string FontColor { get; set; } = "#FFFFFF";
}
