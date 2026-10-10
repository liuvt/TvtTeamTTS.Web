using System.Globalization;
using System.Text;
using System.Text.Json;
using TvtTeamTTS.Web.Models;

namespace TvtTeamTTS.Web.Services;

/// <summary>Phân loại theo thư mục Voices/Nữ, Voices/Nam, Voices/Khác.
/// Voice cũ để trực tiếp trong Voices vẫn hỗ trợ với quy tắc nhận diện tên.</summary>
public sealed class VoiceService(IWebHostEnvironment env)
{
    private static readonly string[] FemaleNames =
    ["ngochuyen", "mytam", "maiphuong", "phuongtrang", "minhthu", "thanhphuong", "banmai", "ngoclan", "minhanh", "thuha", "yen nhi", "yen_nhi", "vais1000"];
    private static readonly string[] MaleNames =
    ["ngocngan", "manhdung", "taian", "minhquang", "thientam", "duyoryx", "chieu", "lacphi", "minhkhang", "tranthanh", "vietthao", "quanghuy", "25hours"];

    public List<VoiceModel> GetVoices()
    {
        var root = Path.Combine(env.ContentRootPath, "Voices");
        Directory.CreateDirectory(root);
        var voices = new List<VoiceModel>();

        foreach (var model in Directory.EnumerateFiles(root, "*.onnx", SearchOption.AllDirectories))
        {
            var config = ResolveConfig(model);
            if (config is null) continue;

            var relative = Path.GetRelativePath(root, model);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folder = Path.GetFileName(Path.GetDirectoryName(model)) ?? "Voice";
            var name = parts.Length == 1 ? Path.GetFileNameWithoutExtension(model) : folder;
            // Nếu chỉ có Voices/Nữ/model.onnx, dùng tên model để tránh hiển thị tên 'Nữ'.
            if (parts.Length == 2 && Normalize(folder) is "nu" or "nam" or "khac")
                name = Path.GetFileNameWithoutExtension(model);
            var category = ResolveCategory(parts, name);
            var language = "vi_VN";
            var quality = "custom";
            var numSpeakers = 1;
            var speakers = new List<VoiceSpeaker>();
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(config));
                var r = doc.RootElement;
                if (r.TryGetProperty("language", out var lang))
                {
                    if (lang.ValueKind == JsonValueKind.Object && lang.TryGetProperty("code", out var code))
                        language = code.GetString() ?? language;
                    else if (lang.ValueKind == JsonValueKind.String)
                        language = lang.GetString() ?? language;
                }
                if (r.TryGetProperty("audio", out var audio) && audio.TryGetProperty("quality", out var q))
                    quality = q.GetString() ?? quality;
                if (r.TryGetProperty("num_speakers", out var ns) && ns.TryGetInt32(out var n))
                    numSpeakers = Math.Max(1, n);
                if (r.TryGetProperty("speaker_id_map", out var map) && map.ValueKind == JsonValueKind.Object)
                {
                    foreach (var item in map.EnumerateObject())
                        if (item.Value.TryGetInt32(out var id)) speakers.Add(new VoiceSpeaker(id, item.Name));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                // Config tùy chỉnh có thể thiếu metadata; vẫn hiển thị model.
            }
            if (speakers.Count == 0 && numSpeakers > 1)
                speakers.AddRange(Enumerable.Range(0, numSpeakers).Select(i => new VoiceSpeaker(i, $"Speaker {i}")));

            voices.Add(new VoiceModel(name, model, config, language, quality, numSpeakers,
                speakers.OrderBy(x => x.Id).ToList(), category));
        }
        return voices.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static string ResolveCategory(string[] parts, string name)
    {
        if (parts.Length >= 2)
        {
            // Thư mục đầu tiên là quy tắc ưu tiên, không đoán theo tên nếu đã phân nhóm.
            var section = Normalize(parts[0]);
            if (section == "nu") return "Nữ";
            if (section == "nam") return "Nam";
            if (section == "khac") return "Khác";
        }
        // Khả năng tương thích: Voices/NgocHuyen/model.onnx hoặc Voices/ngochuyen.onnx.
        var probe = Normalize(string.Join(' ', parts) + " " + name).Replace(" ", "").Replace("_", "");
        if (FemaleNames.Any(x => probe.Contains(Normalize(x).Replace(" ", "").Replace("_", "")))) return "Nữ";
        if (MaleNames.Any(x => probe.Contains(Normalize(x).Replace(" ", "").Replace("_", "")))) return "Nam";
        return "Khác";
    }

    private static string Normalize(string input)
    {
        var decomposed = input.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC);
    }

    private static string? ResolveConfig(string model)
    {
        if (File.Exists(model + ".json")) return model + ".json";
        var generic = Path.Combine(Path.GetDirectoryName(model)!, "config.json");
        return File.Exists(generic) ? generic : null;
    }
}
