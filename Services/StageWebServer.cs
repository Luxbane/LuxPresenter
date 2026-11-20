using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LuxPresenter.Services;

public class StageWebServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ProjectionState _state;
    public int Port { get; private set; } = 8080;
    public bool IsRunning { get; private set; } = false;

    public StageWebServer(ProjectionState state, int port = 8080)
    {
        _state = state;
        Port = port;
    }

    public static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    public string GetStageUrl()
    {
        return $"http://{GetLocalIPAddress()}:{Port}/stage";
    }

    public void Start(int? port = null)
    {
        if (IsRunning) return;
        if (port.HasValue && port.Value > 0) Port = port.Value;

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            IsRunning = true;

            Task.Run(() => AcceptClientsAsync(_cts.Token));
        }
        catch
        {
            IsRunning = false;
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch { }
        finally
        {
            _listener = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client), ct);
            }
            catch
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            try
            {
                string? requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;
                string path = parts[1];

                // Drain remaining request headers
                string? header;
                while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync())) { }

                if (path.StartsWith("/api/status", StringComparison.OrdinalIgnoreCase))
                {
                    // Return JSON status
                    string json = GetStatusJson();
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

                    await writer.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
                    await writer.FlushAsync();
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                }
                else
                {
                    // Return HTML Web Stage Display
                    string html = GetStageHtml();
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(html);

                    await writer.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
                    await writer.FlushAsync();
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                }
            }
            catch { }
        }
    }

    private string GetStatusJson()
    {
        var status = new
        {
            time = DateTime.Now.ToString("hh:mm:ss tt"),
            itemTitle = !string.IsNullOrWhiteSpace(_state.ActiveItemTitle) ? _state.ActiveItemTitle : "Live Presentation",
            currentLabel = _state.ActiveSlide?.Label ?? "",
            currentText = _state.IsBlack ? "[ BLACKOUT ACTIVE ]" :
                          _state.IsClear ? "[ CLEAR ACTIVE ]" :
                          _state.ActiveSlide?.Content ?? "— No Active Slide —",
            nextLabel = _state.ActiveNextSlide?.Label ?? "",
            nextText = _state.ActiveNextSlide?.Content ?? "(End of Song / Last Slide)",
            isBlack = _state.IsBlack,
            isClear = _state.IsClear,
            isLive = _state.IsLive,
            stageMessage = _state.StageMessage
        };

        return JsonSerializer.Serialize(status);
    }

    private static string GetStageHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>LuxPresenter - Wireless Stage Display</title>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    background-color: #000000;
    color: #FFFFFF;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    height: 100vh;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }
  header {
    background: #111827;
    border-bottom: 2px solid #1F2937;
    padding: 12px 20px;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }
  .clock {
    font-size: 1.4rem;
    font-weight: 800;
    color: #38BDF8;
    letter-spacing: 1px;
  }
  .title-bar {
    font-size: 1.1rem;
    font-weight: 600;
    color: #F3F4F6;
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .badge {
    background: #374151;
    color: #FCD34D;
    font-size: 0.8rem;
    font-weight: bold;
    padding: 3px 8px;
    border-radius: 4px;
  }
  .alert-banner {
    display: none;
    background: #DC2626;
    color: #FFFFFF;
    font-size: 1.1rem;
    font-weight: bold;
    padding: 10px 20px;
    text-align: center;
    border-bottom: 2px solid #B91C1C;
  }
  main {
    flex: 1;
    display: flex;
    flex-direction: column;
    padding: 16px;
    gap: 12px;
  }
  .current-card {
    flex: 3;
    background: #0A0F1D;
    border: 2px solid #1E293B;
    border-radius: 12px;
    padding: 20px;
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    text-align: center;
    position: relative;
  }
  .card-label {
    position: absolute;
    top: 12px;
    left: 16px;
    font-size: 0.8rem;
    font-weight: bold;
    letter-spacing: 1px;
    text-transform: uppercase;
  }
  .current-label { color: #F59E0B; }
  .next-label { color: #94A3B8; }
  .current-text {
    font-size: clamp(1.6rem, 4vw, 3.2rem);
    font-weight: 800;
    color: #FEF08A;
    line-height: 1.35;
    white-space: pre-wrap;
  }
  .next-card {
    flex: 2;
    background: #0B132B;
    border: 1.5px solid #1E293B;
    border-radius: 10px;
    padding: 14px 20px;
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    text-align: center;
    position: relative;
  }
  .next-text {
    font-size: clamp(1.1rem, 2.5vw, 1.8rem);
    font-weight: 600;
    color: #CBD5E1;
    line-height: 1.3;
    white-space: pre-wrap;
  }
  .blackout { color: #EF4444 !important; }
</style>
</head>
<body>
  <header>
    <div class=""clock"" id=""clock"">--:--:--</div>
    <div class=""title-bar"">
      <span id=""songTitle"">LuxPresenter</span>
      <span class=""badge"" id=""sectionBadge"">LIVE</span>
    </div>
  </header>

  <div class=""alert-banner"" id=""alertBanner"">
    ⚠️ <span id=""alertText""></span>
  </div>

  <main>
    <div class=""current-card"">
      <div class=""card-label current-label"">▶ CURRENT SLIDE</div>
      <div class=""current-text"" id=""currentText"">Loading...</div>
    </div>
    <div class=""next-card"">
      <div class=""card-label next-label"">⏭ NEXT SLIDE</div>
      <div class=""next-text"" id=""nextText"">...</div>
    </div>
  </main>

  <script>
    async function updateStatus() {
      try {
        const res = await fetch('/api/status');
        if (!res.ok) return;
        const data = await res.json();
        
        document.getElementById('clock').textContent = data.time || '';
        document.getElementById('songTitle').textContent = data.itemTitle || '';
        
        const badge = document.getElementById('sectionBadge');
        if (data.currentLabel) {
          badge.textContent = data.currentLabel;
          badge.style.display = 'inline-block';
        } else {
          badge.style.display = 'none';
        }

        const currentElem = document.getElementById('currentText');
        currentElem.textContent = data.currentText || '';
        if (data.isBlack || data.isClear) {
          currentElem.classList.add('blackout');
        } else {
          currentElem.classList.remove('blackout');
        }

        const nextElem = document.getElementById('nextText');
        nextElem.textContent = data.nextText || '';

        const alertBanner = document.getElementById('alertBanner');
        if (data.stageMessage) {
          document.getElementById('alertText').textContent = data.stageMessage;
          alertBanner.style.display = 'block';
        } else {
          alertBanner.style.display = 'none';
        }
      } catch (e) {}
    }

    setInterval(updateStatus, 500);
    updateStatus();
  </script>
</body>
</html>";
    }

    public void Dispose()
    {
        Stop();
    }
}
