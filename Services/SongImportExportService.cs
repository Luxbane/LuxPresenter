using System.IO;
using System.Text;
using System.Text.Json;
using LuxPresenter.Models;

namespace LuxPresenter.Services;

/// <summary>
/// Provides native LuxPresenter song import and export services.
/// Uses standard UTF-8 JSON format for maximum compatibility and open portability.
/// </summary>
public static class SongImportExportService
{
    /// <summary>
    /// Exports all songs into a portable JSON backup file.
    /// </summary>
    public static void ExportSongsToJson(string destinationPath, IEnumerable<Song> songs)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(songs, options);
        File.WriteAllText(destinationPath, json, Encoding.UTF8);
    }

    /// <summary>
    /// Imports songs from a JSON backup file.
    /// </summary>
    public static List<Song> ImportSongsFromJson(string sourcePath)
    {
        if (!File.Exists(sourcePath)) return new List<Song>();
        var json = File.ReadAllText(sourcePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<List<Song>>(json) ?? new List<Song>();
    }
}
