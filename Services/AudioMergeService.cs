using System.Diagnostics;

namespace TvtTeamTTS.Web.Services;

public sealed class AudioMergeService(IWebHostEnvironment env)
{
    public async Task MergeAsync(IReadOnlyList<string> wavs, string mp3, CancellationToken ct)
    {
        if (wavs.Count == 0) throw new InvalidOperationException("Không có dữ liệu âm thanh để ghép.");
        var exe = Path.Combine(env.ContentRootPath, "Runtime", "ffmpeg", "ffmpeg.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("Thiếu Runtime/ffmpeg/ffmpeg.exe", exe);

        var list = Path.Combine(Path.GetDirectoryName(wavs[0])!, "concat.txt");
        await File.WriteAllLinesAsync(list, wavs.Select(x => $"file '{x.Replace("\\", "/").Replace("'", "'\\''")}'"), ct);
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", list,
                     "-c:a", "libmp3lame", "-b:a", "192k", mp3 })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Không khởi động được bộ ghép MP3.");
        try
        {
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(ct);
            var log = await stderr;
            if (process.ExitCode != 0) throw new InvalidOperationException($"Không ghép được MP3 ({process.ExitCode}): {log}");
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* Tiến trình vừa kết thúc. */ }
                try { await process.WaitForExitAsync(); }
                catch (InvalidOperationException) { }
            }
            throw;
        }
    }
}
