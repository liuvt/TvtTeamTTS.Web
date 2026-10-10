using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using TvtTeamTTS.Web.Models;

namespace TvtTeamTTS.Web.Services;

/// <summary>Engine tổng hợp âm thanh offline. Tên file runtime được giữ để tương thích bản cũ.</summary>
public sealed class TtsEngineService(IWebHostEnvironment env, NativePathService nativePaths,
    ILogger<TtsEngineService> logger)
{
    private const int StackBufferOverrun = unchecked((int)0xC0000409);

    public async Task RenderAsync(string text, VoiceModel voice, string wav, double speed, int? speakerId, CancellationToken ct)
    {
        var exe = Path.Combine(env.ContentRootPath, "Runtime", "piper", "piper.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("Thiếu bộ xử lý giọng nói tại Runtime/piper/piper.exe", exe);
        if (!AllowedSpeeds.Contains(speed)) throw new ArgumentOutOfRangeException(nameof(speed), "Tốc độ phát video không hợp lệ.");
        ValidateConfig(voice, speakerId);
        var (modelPath, configPath) = nativePaths.PrepareVoice(voice);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(false),
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(modelPath);
        psi.ArgumentList.Add("--config"); psi.ArgumentList.Add(configPath);
        psi.ArgumentList.Add("--output_file"); psi.ArgumentList.Add(wav);
        psi.ArgumentList.Add("--length_scale");
        psi.ArgumentList.Add((1.0 / speed).ToString("G17", CultureInfo.InvariantCulture));
        if (speakerId.HasValue)
        {
            psi.ArgumentList.Add("--speaker");
            psi.ArgumentList.Add(speakerId.Value.ToString(CultureInfo.InvariantCulture));
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Không khởi động được bộ tạo giọng đọc.");
        try
        {
            var stderrTask = process.StandardError.ReadToEndAsync();
            IOException? stdinError = null;
            try
            {
                await process.StandardInput.WriteAsync(text.AsMemory(), ct);
                await process.StandardInput.WriteLineAsync(); // Kết thúc dòng để engine nhận input.
                await process.StandardInput.FlushAsync(ct);
            }
            catch (IOException ex)
            {
                // Engine có thể crash trong lúc nhận input; chờ lấy exit code thật.
                stdinError = ex;
            }
            finally
            {
                try { process.StandardInput.Close(); }
                catch (IOException) { }
            }
            await process.WaitForExitAsync(ct);
            var log = await stderrTask;
            if (process.ExitCode != 0)
            {
                logger.LogError("Voice engine failed. Exit={ExitCode}, voice={Voice}, group={Group}, model={Model}, config={Config}, log={Log}",
                    process.ExitCode, voice.Name, voice.Category, modelPath, configPath, log);
                if (process.ExitCode == StackBufferOverrun)
                    throw new InvalidOperationException(
                        $"Engine giọng đọc gặp lỗi Windows 0xC0000409 (nhóm {voice.Category}, voice {voice.Name}). " +
                        "Ứng dụng đã sử dụng đường dẫn model an toàn không dấu, nhưng engine vẫn dừng bất thường. " +
                        "Hãy kiểm tra model ONNX có đúng cặp JSON và tương thích với phiên bản engine, " +
                        "các DLL trong Runtime/piper và xem log CMD. " +
                        (string.IsNullOrWhiteSpace(log) ? "" : $"Chi tiết: {Truncate(log, 750)}"));
                throw new InvalidOperationException($"Engine giọng đọc báo lỗi ({process.ExitCode}) " +
                    $"với giọng {voice.Name}: {Truncate(log, 1000)}");
            }
            if (stdinError is not null)
                throw new InvalidOperationException($"Không gửi được văn bản vào engine cho giọng {voice.Name}: {stdinError.Message}", stdinError);
            if (!File.Exists(wav) || new FileInfo(wav).Length <= 44)
                throw new InvalidOperationException($"Engine kết thúc nhưng không tạo WAV hợp lệ cho giọng {voice.Name}. " +
                    $"Vui lòng kiểm tra ONNX/JSON. {Truncate(log, 350)}");
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                try { await process.WaitForExitAsync(); }
                catch (InvalidOperationException) { }
            }
            throw;
        }
    }

    private static void ValidateConfig(VoiceModel voice, int? speakerId)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(voice.ConfigPath, Encoding.UTF8));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Nội dung JSON không phải object.");
            if (root.TryGetProperty("num_speakers", out var number) && number.ValueKind == JsonValueKind.Number &&
                number.TryGetInt32(out var count) && speakerId is int id && (id < 0 || id >= count))
                throw new InvalidOperationException($"Speaker {id} không hợp lệ cho model có {count} speaker.");
            // Không áp đặt schema riêng: có nhiều biến thể model/config tương thích.
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"File JSON của giọng {voice.Name} không hợp lệ: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Không đọc được JSON của giọng {voice.Name}: {ex.Message}", ex);
        }
    }

    private static string Truncate(string? s, int max) => s is null ? "" : s.Length <= max ? s : s[..max] + "...";

    public static readonly HashSet<double> AllowedSpeeds = [0.25, 0.5, 0.75, 1, 1.25, 1.75, 2];
}
