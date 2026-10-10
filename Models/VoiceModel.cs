namespace TvtTeamTTS.Web.Models;

public sealed record VoiceSpeaker(int Id, string Name);

public sealed record VoiceModel(
    string Name,
    string ModelPath,
    string ConfigPath,
    string Language,
    string Quality,
    int NumSpeakers,
    IReadOnlyList<VoiceSpeaker> Speakers,
    string Category)
{
    public bool IsMultiSpeaker => NumSpeakers > 1;
}
