using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TvtTeamTTS.Web.Services;

/// <summary>Mỗi tab có một session ngẫu nhiên. Dọn Output khi tab đóng,
/// hoặc khi session đã không hoạt động quá hạn (phòng trường hợp beacon thất bại).</summary>
public sealed class SessionOutputService(IWebHostEnvironment env, ILogger<SessionOutputService> logger) : BackgroundService
{
    private sealed class State
    {
        public readonly object Gate = new();
        public DateTimeOffset LastTouched = DateTimeOffset.UtcNow;
        public bool Closed;
    }
    private readonly ConcurrentDictionary<string, State> _sessions = new();
    private static readonly TimeSpan MaxIdle = TimeSpan.FromHours(2);

    public static bool ValidId(string? id) => id is not null && Regex.IsMatch(id, "^[a-fA-F0-9]{32}$");
    public static bool ValidFilename(string? file) => file is not null && Regex.IsMatch(file, "^TVT-Team-[a-zA-Z0-9_-]+\\.mp3$");

    public void Register(string id)
    {
        if (!ValidId(id)) throw new ArgumentException("Phiên không hợp lệ.", nameof(id));
        _sessions.TryAdd(id, new State());
    }

    public void EnsureOpen(string id)
    {
        if (!_sessions.TryGetValue(id, out var state)) throw new OperationCanceledException("Phiên làm việc đã kết thúc.");
        lock (state.Gate)
        {
            if (state.Closed) throw new OperationCanceledException("Tab đã đóng.");
            state.LastTouched = DateTimeOffset.UtcNow;
        }
    }

    public string OutputPath(string id, string filename)
    {
        if (!ValidId(id) || !ValidFilename(filename)) throw new ArgumentException("Tên file không hợp lệ.");
        return Path.Combine(env.ContentRootPath, "Output", id, filename);
    }

    public void FinishOutput(string id, string outputFile)
    {
        try { EnsureOpen(id); }
        catch { TryDeleteFile(outputFile); throw; }
    }

    public void Close(string id)
    {
        if (!ValidId(id)) return;
        if (_sessions.TryGetValue(id, out var state))
        {
            lock (state.Gate) state.Closed = true;
        }
        TryDeleteSessionDirectory(id);
    }

    private void TryDeleteSessionDirectory(string id)
    {
        try
        {
            var dir = Path.Combine(env.ContentRootPath, "Output", id);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (IOException ex) { logger.LogDebug(ex, "Output đang được trình duyệt đọc: {Session}", id); }
        catch (UnauthorizedAccessException ex) { logger.LogWarning(ex, "Không xóa được output: {Session}", id); }
    }

    private static void TryDeleteFile(string filename)
    {
        try { if (File.Exists(filename)) File.Delete(filename); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = DateTimeOffset.UtcNow;
                foreach (var item in _sessions)
                {
                    bool expires;
                    lock (item.Value.Gate)
                    {
                        expires = now - item.Value.LastTouched > MaxIdle;
                        if (expires) item.Value.Closed = true;
                    }
                    if (expires)
                    {
                        TryDeleteSessionDirectory(item.Key);
                        _sessions.TryRemove(item.Key, out _);
                    }
                }
                // Bao gồm session mất theo dõi do ứng dụng bị restart.
                var dir = Path.Combine(env.ContentRootPath, "Output");
                if (!Directory.Exists(dir)) continue;
                foreach (var sessionDir in Directory.EnumerateDirectories(dir))
                {
                    var id = Path.GetFileName(sessionDir);
                    if (!ValidId(id) || _sessions.ContainsKey(id)) continue;
                    try
                    {
                        if (now - Directory.GetLastWriteTimeUtc(sessionDir) > MaxIdle) Directory.Delete(sessionDir, true);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
