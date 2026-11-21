using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using LuxPresenter.Services;

namespace LuxPresenter.Views;

public partial class UpdateDialog : Window
{
    private static readonly Regex InlineTokenRegex = new Regex(
        @"(?<link>\[(?<linkText>[^\]]+)\]\((?<linkUrl>https?://[^\s\)]+)\))|" +
        @"(?<bold>\*\*(?<boldText>.*?)\*\*|__(?<boldText>.*?)__)|" +
        @"(?<code>`(?<codeText>.*?)`)|" +
        @"(?<italic>\*(?<italicText>.*?)\*|_(?<italicText>.*?)_)|" +
        @"(?<bareurl>https?://[^\s\<\>""\)]+)",
        RegexOptions.Compiled);

    private readonly GitHubReleaseInfo _release;

    public UpdateDialog(GitHubReleaseInfo release, string currentVersion)
    {
        InitializeComponent();
        _release = release;

        TxtVersionSub.Text = $"Current: v{currentVersion}  →  New: {release.TagName}";

        DocViewerChangelog.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnHyperlinkRequestNavigate));

        string rawBody = string.IsNullOrWhiteSpace(release.Body)
            ? $"### LuxPresenter {release.TagName}\r\nA new version is available for download."
            : release.Body;

        DocViewerChangelog.Document = BuildMarkdownDocument(rawBody);
    }

    private FlowDocument BuildMarkdownDocument(string markdown)
    {
        var doc = new FlowDocument
        {
            Background = Brushes.Transparent,
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI, Tahoma, sans-serif"),
            FontSize = 12,
            ColumnWidth = double.PositiveInfinity
        };
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "TextPrimaryBrush");

        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (doc.Blocks.Count > 0 && doc.Blocks.LastBlock is Paragraph lastP)
                {
                    lastP.Margin = new Thickness(lastP.Margin.Left, lastP.Margin.Top, lastP.Margin.Right, lastP.Margin.Bottom + 4);
                }
                continue;
            }

            // Horizontal rule
            if (trimmed == "---" || trimmed == "***" || trimmed == "___" || Regex.IsMatch(trimmed, @"^[-*_]{3,}$"))
            {
                var hr = new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 8, 0, 8),
                    SnapsToDevicePixels = true
                };
                hr.SetResourceReference(Border.BackgroundProperty, "BorderBrush");
                doc.Blocks.Add(new BlockUIContainer(hr));
                continue;
            }

            // Headings
            if (trimmed.StartsWith("#### "))
            {
                var p = new Paragraph
                {
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 6, 0, 2)
                };
                p.SetResourceReference(Paragraph.ForegroundProperty, "TextPrimaryBrush");
                AppendInlines(p.Inlines, trimmed.Substring(5).Trim());
                doc.Blocks.Add(p);
                continue;
            }
            if (trimmed.StartsWith("### "))
            {
                var p = new Paragraph
                {
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 3)
                };
                p.SetResourceReference(Paragraph.ForegroundProperty, "TextPrimaryBrush");
                AppendInlines(p.Inlines, trimmed.Substring(4).Trim());
                doc.Blocks.Add(p);
                continue;
            }
            if (trimmed.StartsWith("## "))
            {
                var p = new Paragraph
                {
                    FontSize = 14.5,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 4)
                };
                p.SetResourceReference(Paragraph.ForegroundProperty, "TextPrimaryBrush");
                AppendInlines(p.Inlines, trimmed.Substring(3).Trim());
                doc.Blocks.Add(p);
                continue;
            }
            if (trimmed.StartsWith("# "))
            {
                var p = new Paragraph
                {
                    FontSize = 16.5,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 12, 0, 6)
                };
                p.SetResourceReference(Paragraph.ForegroundProperty, "TextPrimaryBrush");
                AppendInlines(p.Inlines, trimmed.Substring(2).Trim());
                doc.Blocks.Add(p);
                continue;
            }

            // Blockquote
            if (trimmed.StartsWith("> "))
            {
                var quoteBorder = new Border
                {
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(8, 2, 0, 2),
                    Margin = new Thickness(6, 4, 0, 4)
                };
                quoteBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

                var quoteText = new TextBlock
                {
                    FontStyle = FontStyles.Italic,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11.5
                };
                quoteText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                AppendInlines(quoteText.Inlines, trimmed.Substring(2).Trim());
                quoteBorder.Child = quoteText;

                doc.Blocks.Add(new BlockUIContainer(quoteBorder));
                continue;
            }

            // Bullet list item
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
            {
                var p = new Paragraph
                {
                    Margin = new Thickness(14, 1.5, 0, 2),
                    TextIndent = -10
                };
                p.SetResourceReference(Paragraph.ForegroundProperty, "TextPrimaryBrush");

                var bullet = new Run("•  ") { FontWeight = FontWeights.Bold };
                bullet.SetResourceReference(Run.ForegroundProperty, "TextSecondaryBrush");
                p.Inlines.Add(bullet);

                AppendInlines(p.Inlines, trimmed.Substring(2).Trim());
                doc.Blocks.Add(p);
                continue;
            }

            // Numbered list item
            var numMatch = Regex.Match(trimmed, @"^(\d+)\.\s+(.*)$");
            if (numMatch.Success)
            {
                var p = new Paragraph
                {
                    Margin = new Thickness(16, 1.5, 0, 2),
                    TextIndent = -12
                };
                p.SetResourceReference(Paragraph.ForegroundProperty, "TextPrimaryBrush");

                var numRun = new Run(numMatch.Groups[1].Value + ". ") { FontWeight = FontWeights.SemiBold };
                numRun.SetResourceReference(Run.ForegroundProperty, "TextSecondaryBrush");
                p.Inlines.Add(numRun);

                AppendInlines(p.Inlines, numMatch.Groups[2].Value);
                doc.Blocks.Add(p);
                continue;
            }

            // Standard paragraph
            var regularP = new Paragraph
            {
                Margin = new Thickness(0, 2, 0, 4)
            };
            regularP.SetResourceReference(Paragraph.ForegroundProperty, "TextPrimaryBrush");
            AppendInlines(regularP.Inlines, line);
            doc.Blocks.Add(regularP);
        }

        return doc;
    }

    private void AppendInlines(InlineCollection inlines, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var matches = InlineTokenRegex.Matches(text);
        int lastIndex = 0;

        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                string plain = text.Substring(lastIndex, match.Index - lastIndex);
                inlines.Add(new Run(plain));
            }

            if (match.Groups["link"].Success)
            {
                string linkText = match.Groups["linkText"].Value;
                string linkUrl = match.Groups["linkUrl"].Value;
                inlines.Add(CreateHyperlink(linkText, linkUrl));
            }
            else if (match.Groups["bareurl"].Success)
            {
                string url = match.Groups["bareurl"].Value;
                inlines.Add(CreateHyperlink(url, url));
            }
            else if (match.Groups["bold"].Success)
            {
                string boldText = match.Groups["boldText"].Value;
                inlines.Add(new Bold(new Run(boldText)));
            }
            else if (match.Groups["code"].Success)
            {
                string codeText = match.Groups["codeText"].Value;
                var codeRun = new Run(" " + codeText + " ")
                {
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    Background = new SolidColorBrush(Color.FromArgb(38, 128, 128, 128)),
                    FontSize = 11.5
                };
                inlines.Add(codeRun);
            }
            else if (match.Groups["italic"].Success)
            {
                string italicText = match.Groups["italicText"].Value;
                inlines.Add(new Italic(new Run(italicText)));
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            inlines.Add(new Run(text.Substring(lastIndex)));
        }
    }

    private static Hyperlink CreateHyperlink(string text, string url)
    {
        var hyperlink = new Hyperlink(new Run(text))
        {
            ToolTip = url,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)), // #38BDF8 Sky blue
            TextDecorations = TextDecorations.Underline,
            Cursor = Cursors.Hand
        };

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            hyperlink.NavigateUri = uri;
        }

        return hyperlink;
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            if (e.Uri != null)
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to navigate link: {ex.Message}");
        }
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string url = !string.IsNullOrWhiteSpace(_release.DownloadUrl) ? _release.DownloadUrl : _release.HtmlUrl;
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open download link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
