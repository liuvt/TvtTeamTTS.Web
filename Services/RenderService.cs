using TvtTeamTTS.Web.Models;

namespace TvtTeamTTS.Web.Services;

public sealed class RenderService(
    NativePathService nativePaths,
    TextSplitterService splitter,
    TtsEngineService engine,
    AudioMergeService audio,
    SessionOutputService sessions)
{
    public const int MaxCharacters = 50_000;

    public async Task<string> RenderAsync(
        string text, VoiceModel voice, double speed, int? speakerId, string sessionId,
        IProgress<(int done, int total)>? progress, CancellationToken ct)
    {
        if (splitter.CharacterCount(text) > MaxCharacters)
            throw new InvalidOperationException("Nội dung vượt quá giới hạn 50.000 ký tự.");
        if (!TtsEngineService.AllowedSpeeds.Contains(speed))
            throw new InvalidOperationException("Tốc độ phát video không nằm trong danh sách hỗ trợ.");
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Vui lòng nhập văn bản trước khi tạo MP3.");
        sessions.EnsureOpen(sessionId);
        var chunks = splitter.Split(text);
        if (chunks.Count == 0) throw new InvalidOperationException("Không tìm thấy nội dung hợp lệ.");

        var job = nativePaths.NewJobDirectory();
        var wavs = new List<string>();
        try
        {
            for (var i = 0; i < chunks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                sessions.EnsureOpen(sessionId);
                var wav = Path.Combine(job, $"chunk_{i + 1:0000}.wav");
                await engine.RenderAsync(chunks[i], voice, wav, speed, speakerId, ct);
                wavs.Add(wav);
                progress?.Report((i + 1, chunks.Count));
            }
            ct.ThrowIfCancellationRequested();
            sessions.EnsureOpen(sessionId);
            var filename = $"TVT-Team-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.mp3";
            var output = sessions.OutputPath(sessionId, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            try
            {
                await audio.MergeAsync(wavs, output, ct);
                sessions.FinishOutput(sessionId, output);
                return filename;
            }
            catch
            {
                try { if (File.Exists(output)) File.Delete(output); } catch (IOException) { }
                throw;
            }
        }
        finally
        {
            try { Directory.Delete(job, true); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
