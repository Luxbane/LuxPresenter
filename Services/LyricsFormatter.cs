using System;
using System.Linq;
using System.Text.RegularExpressions;
using LuxPresenter.Models;

namespace LuxPresenter.Services;

public static class LyricsFormatter
{
    public static string Format(string? raw, AppSettings? settings)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        if (settings == null) return raw;

        string text = raw;

        // 1. Capitalize All Words (ALL CAPS)
        if (settings.CapitalizeAllWords)
        {
            return text.ToUpperInvariant();
        }

        // 2. Capitalize first character on each line
        if (settings.CapitalizeFirstCharEachLine)
        {
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!string.IsNullOrEmpty(line))
                {
                    int firstLetterIdx = -1;
                    for (int j = 0; j < line.Length; j++)
                    {
                        if (char.IsLetter(line[j]))
                        {
                            firstLetterIdx = j;
                            break;
                        }
                    }
                    if (firstLetterIdx >= 0)
                    {
                        lines[i] = line.Substring(0, firstLetterIdx) +
                                   char.ToUpper(line[firstLetterIdx]) +
                                   line.Substring(firstLetterIdx + 1);
                    }
                }
            }
            text = string.Join("\n", lines);
        }

        // 3. Automatically capitalize first character of these words (keywords)
        if (settings.AutoCapitalizeKeywords && !string.IsNullOrWhiteSpace(settings.CapitalizeKeywords))
        {
            var keywords = settings.CapitalizeKeywords
                .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            foreach (var kw in keywords)
            {
                string pattern = $@"\b{Regex.Escape(kw)}\b";
                text = Regex.Replace(text, pattern, m =>
                {
                    var word = m.Value;
                    if (word.Length == 1) return word.ToUpper();
                    return char.ToUpper(word[0]) + word.Substring(1);
                }, RegexOptions.IgnoreCase);
            }
        }

        return text;
    }
}
