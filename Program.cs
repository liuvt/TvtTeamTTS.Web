using TvtTeamTTS.Web.Components;
using TvtTeamTTS.Web.Services;

var builder = WebApplication.CreateBuilder(args);
// Truyền toàn bộ văn bản tối đa 50.000 ký tự MỘT LẦN khi bấm render.
// Văn bản tiếng Việt UTF-8/JSON có thể vượt mặc định 32 KB của SignalR.
// Giới hạn 512 KiB được dùng cho lời gọi JS->.NET, không gửi text theo oninput.
builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 512 * 1024);
builder.Services.AddSingleton<VoiceService>();
builder.Services.AddSingleton<TextSplitterService>();
builder.Services.AddSingleton<NativePathService>();
builder.Services.AddSingleton<TtsEngineService>();
builder.Services.AddSingleton<AudioMergeService>();
builder.Services.AddSingleton<SessionOutputService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SessionOutputService>());
builder.Services.AddSingleton<RenderService>();

var app = builder.Build();
foreach (var path in new[] { "Temp", "Output", "Voices" })
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, path));

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/download/{sessionId}/{file}", (string sessionId, string file, SessionOutputService outputs, HttpContext http) =>
{
    if (!SessionOutputService.ValidId(sessionId) || !SessionOutputService.ValidFilename(file))
        return Results.NotFound();
    var path = outputs.OutputPath(sessionId, file);
    if (!File.Exists(path)) return Results.NotFound();
    http.Response.Headers["Cache-Control"] = "private, no-store, max-age=0";
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return Results.File(path, "audio/mpeg", fileDownloadName: file, enableRangeProcessing: true);
});

// navigator.sendBeacon khi đóng tab. Mỗi session dùng token ngẫu nhiên khó đoán.
app.MapPost("/api/session/{sessionId}/touch", (string sessionId, SessionOutputService outputs) =>
{
    if (!SessionOutputService.ValidId(sessionId)) return Results.BadRequest();
    try { outputs.EnsureOpen(sessionId); return Results.NoContent(); }
    catch (OperationCanceledException) { return Results.NotFound(); }
});

app.MapPost("/api/session/{sessionId}/close", (string sessionId, SessionOutputService outputs) =>
{
    if (!SessionOutputService.ValidId(sessionId)) return Results.BadRequest();
    outputs.Close(sessionId);
    return Results.NoContent();
});

app.Run();
