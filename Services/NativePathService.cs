using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TvtTeamTTS.Web.Models;

namespace TvtTeamTTS.Web.Services;

/// <summary>
/// Một số bản engine TTS native trên Windows không xử lý được đường dẫn model có dấu
/// (Voices/Nữ, Voices/Khác). Giữ nguyên tên thư mục trên UI, nhưng dùng alias ASCII
/// khi giao tiếp với engine. Dùng hardlink nếu có thể để tránh nhân đôi model lớn.
/// </summary>
public sealed class NativePathService(IWebHostEnvironment environment, IConfiguration configuration,
    ILogger<NativePathService> logger)
{
    private readonly object _cacheGate = new();
    private string? _workRoot;

    public string WorkRoot
    {
        get
        {
            lock (_cacheGate)
            {
                return _workRoot ??= CreateWorkRoot();
            }
        }
    }

    public string NewJobDirectory()
    {
        var dir = Path.Combine(WorkRoot, "jobs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <returns>Cặp đường dẫn (model, cấu hình) an toàn với các engine dùng argv ANSI.</returns>
    public (string Model, string Config) PrepareVoice(VoiceModel voice)
    {
        if (!File.Exists(voice.ModelPath))
            throw new FileNotFoundException("Không tìm thấy file model ONNX của giọng đã chọn.", voice.ModelPath);
        if (!File.Exists(voice.ConfigPath))
            throw new FileNotFoundException("Không tìm thấy file cấu hình JSON tương ứng với ONNX.", voice.ConfigPath);

        if (IsAscii(voice.ModelPath) && IsAscii(voice.ConfigPath))
            return (voice.ModelPath, voice.ConfigPath);

        var modelInfo = new FileInfo(voice.ModelPath);
        var configInfo = new FileInfo(voice.ConfigPath);
        var fingerprint = string.Join("|", Path.GetFullPath(voice.ModelPath), modelInfo.Length,
            modelInfo.LastWriteTimeUtc.Ticks, Path.GetFullPath(voice.ConfigPath), configInfo.Length,
            configInfo.LastWriteTimeUtc.Ticks);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)))[..20].ToLowerInvariant();
        var targetDir = Path.Combine(WorkRoot, "voice-cache", id);
        var modelAlias = Path.Combine(targetDir, "voice.onnx");
        var configAlias = Path.Combine(targetDir, "voice.onnx.json");

        lock (_cacheGate)
        {
            Directory.CreateDirectory(targetDir);
            LinkOrCopy(voice.ModelPath, modelAlias);
            LinkOrCopy(voice.ConfigPath, configAlias);
        }
        logger.LogInformation("Đã chuẩn bị đường dẫn tương thích cho model {Name} (nhóm {Category}).", voice.Name, voice.Category);
        return (modelAlias, configAlias);
    }

    private static bool IsAscii(string value) => value.All(c => c <= 127);

    private string CreateWorkRoot()
    {
        var configured = configuration["TvtTeamTTS:NativeWorkDirectory"];
        var projectTemp = Path.Combine(environment.ContentRootPath, "Temp");
        var systemTemp = Path.Combine(Path.GetTempPath(), "TVT-Team-TTS");
        var path = !string.IsNullOrWhiteSpace(configured) ? configured
            : IsAscii(projectTemp) ? projectTemp
            : systemTemp;
        path = Path.GetFullPath(path);

        if (!IsAscii(path))
            throw new InvalidOperationException("Đường dẫn thư mục làm việc của engine có ký tự có dấu. " +
                "Hãy tạo thư mục C:\\TVT-TTS-Cache và cấu hình TvtTeamTTS:NativeWorkDirectory " +
                "thành C:\\TVT-TTS-Cache (trong appsettings.json hoặc biến môi trường). ");

        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, "jobs"));
        Directory.CreateDirectory(Path.Combine(path, "voice-cache"));
        return path;
    }

    private static void LinkOrCopy(string source, string target)
    {
        if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(source).Length) return;
        var partial = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Hardlink: cùng phân vùng NTFS, không tốn thêm dung lượng cho model ONNX.
            if (!(OperatingSystem.IsWindows() && CreateHardLink(partial, source, IntPtr.Zero)))
                File.Copy(source, partial, overwrite: false);

            if (File.Exists(target)) File.Delete(target);
            File.Move(partial, target);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}
