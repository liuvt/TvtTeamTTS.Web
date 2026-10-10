using System.Text.RegularExpressions;

namespace TvtTeamTTS.Web.Services;

public sealed class TextSplitterService
{
    // Đếm từ Unicode thực sự: hỗ trợ đầy đủ tiếng Việt, kể cả Unicode tổ hợp.
    // Dấu câu đứng riêng không được tính là từ.
    private static readonly Regex WordRegex = new(
        @"[\p{L}\p{M}\p{N}]+(?:['’\-][\p{L}\p{M}\p{N}]+)*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public int WordCount(string? text)
        => string.IsNullOrWhiteSpace(text) ? 0 : WordRegex.Matches(text).Count;

    // Tổng số ký tự người dùng đã nhập, gồm cả khoảng trắng/xuống dòng.
    public int CharacterCount(string? text) => text?.Length ?? 0;

    // Số ký tự nội dung, không tính whitespace. Hữu ích để đối chiếu text thực.
    public int NonWhitespaceCharacterCount(string? text)
        => string.IsNullOrEmpty(text) ? 0 : text.Count(c => !char.IsWhiteSpace(c));

    public List<string> Split(string text, int maxChars = 1500)
    {
        var sentences = Regex.Split(text.Trim(), @"(?<=[.!?…])\s+|\r?\n+")
            .Where(x => !string.IsNullOrWhiteSpace(x));

        var chunks = new List<string>();
        var cur = "";

        foreach (var s in sentences)
        {
            if (s.Length > maxChars)
            {
                if (cur.Length > 0)
                {
                    chunks.Add(cur.Trim());
                    cur = "";
                }

                for (int i = 0; i < s.Length; i += maxChars)
                    chunks.Add(s.Substring(i, Math.Min(maxChars, s.Length - i)));

                continue;
            }

            if (cur.Length + s.Length + 1 > maxChars)
            {
                chunks.Add(cur.Trim());
                cur = s;
            }
            else
            {
                cur += (cur.Length == 0 ? "" : " ") + s;
            }
        }

        if (cur.Length > 0)
            chunks.Add(cur.Trim());

        return chunks;
    }
}
