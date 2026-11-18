using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LuxPresenter.Services.Presentation;

public class LibreOfficeSlideshowService : IDisposable
{
    private static LibreOfficeSlideshowService? _instance;
    public static LibreOfficeSlideshowService Instance => _instance ??= new LibreOfficeSlideshowService();

    private Process? _process;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private int _requestId = 0;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsRunning { get; private set; } = false;
    public int CurrentSlideIndex { get; private set; } = 0;
    public int TotalSlides { get; private set; } = 0;

    public LibreOfficeSlideshowService()
    {
    }

    public async Task<bool> EnsureBridgeConnectedAsync(int port = 2002)
    {
        if (_isInitialized && _process != null && !_process.HasExited)
        {
            return true;
        }

        await _lock.WaitAsync();
        try
        {
            if (_isInitialized && _process != null && !_process.HasExited)
            {
                return true;
            }

            string loDir = AppPaths.ResolveLibreOfficeDirectory();
            string pythonExe = Path.Combine(loDir, "program", "python.exe");
            if (!File.Exists(pythonExe))
            {
                pythonExe = @"E:\LibreOffice\program\python.exe";
            }

            if (!File.Exists(pythonExe))
            {
                Debug.WriteLine($"[LibreOfficeSlideshowService] python.exe not found at {pythonExe}");
                return false;
            }

            string bridgeScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "impress_bridge.py");
            if (!File.Exists(bridgeScript))
            {
                string devPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "impress_bridge.py");
                if (File.Exists(devPath)) bridgeScript = devPath;
            }

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{bridgeScript}\" {port}",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = Process.Start(psi);
            if (_process == null) return false;

            _writer = _process.StandardInput;
            _reader = _process.StandardOutput;

            // Wait for handshake
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var readTask = _reader.ReadLineAsync(cts.Token).AsTask();
            var line = await readTask;
            if (string.IsNullOrWhiteSpace(line)) return false;

            using var doc = JsonDocument.Parse(line);
            var status = doc.RootElement.GetProperty("status").GetString();
            _isInitialized = (status == "ready");
            return _isInitialized;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibreOfficeSlideshowService Error] {ex.Message}");
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<JsonElement> SendCommandAsync(string action, object? parameters = null)
    {
        bool ready = await EnsureBridgeConnectedAsync();
        if (!ready || _writer == null || _reader == null)
        {
            throw new InvalidOperationException("LibreOffice presentation bridge is not available.");
        }

        await _lock.WaitAsync();
        try
        {
            int id = Interlocked.Increment(ref _requestId);
            var req = new Dictionary<string, object>
            {
                ["action"] = action,
                ["id"] = id
            };

            if (parameters != null)
            {
                var json = JsonSerializer.Serialize(parameters);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        req[kvp.Key] = kvp.Value;
                    }
                }
            }

            var requestLine = JsonSerializer.Serialize(req);
            await _writer.WriteLineAsync(requestLine);
            await _writer.FlushAsync();

            var responseLine = await _reader.ReadLineAsync();
            if (responseLine == null)
            {
                throw new IOException("Bridge closed connection unexpectedly.");
            }

            using var doc = JsonDocument.Parse(responseLine);
            var root = doc.RootElement.Clone();

            if (root.TryGetProperty("current_slide", out var cs))
            {
                CurrentSlideIndex = cs.GetInt32();
            }

            return root;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> LoadPresentationAsync(string filePath)
    {
        var res = await SendCommandAsync("load", new { path = filePath });
        if (res.TryGetProperty("slide_count", out var count))
        {
            TotalSlides = count.GetInt32();
            return TotalSlides;
        }
        return 0;
    }

    public async Task StartSlideshowAsync(int display = 1, bool fullscreen = false, int initialSlide = 0)
    {
        await SendCommandAsync("start", new { display, fullscreen, slide = initialSlide });
        IsRunning = true;
    }

    public async Task NextSlideAsync()
    {
        if (IsRunning) await SendCommandAsync("next_slide");
    }

    public async Task PreviousSlideAsync()
    {
        if (IsRunning) await SendCommandAsync("prev_slide");
    }

    public async Task GotoSlideAsync(int slideIndex)
    {
        if (IsRunning) await SendCommandAsync("goto_slide", new { index = slideIndex });
    }

    public async Task BlankScreenAsync(int color = 0)
    {
        if (IsRunning) await SendCommandAsync("blank", new { color });
    }

    public async Task ResumeScreenAsync()
    {
        if (IsRunning) await SendCommandAsync("resume");
    }

    public async Task StopSlideshowAsync()
    {
        if (IsRunning)
        {
            try { await SendCommandAsync("stop"); } catch { }
            IsRunning = false;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_writer != null)
            {
                _writer.WriteLine("{\"action\":\"quit\"}");
                _writer.Flush();
            }
            _process?.WaitForExit(2000);
        }
        catch { }
        finally
        {
            try { _process?.Kill(); } catch { }
            _process?.Dispose();
            _process = null;
            _isInitialized = false;
            IsRunning = false;
        }
    }
}
