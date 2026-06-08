using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ImageAI.Models;

namespace ImageAI.Services;

public class LlmService
{
    private readonly HttpClient _http;
    private readonly string     _baseUrl;
    private readonly string     _model;

    private const string SystemPrompt = """
        You are a friendly AI image-processing assistant in a chat interface.
        Convert the user's natural-language request into a single JSON object.
        Return ONLY valid JSON — no markdown, no code fences, no explanation.

        IMPORTANT: always include a "reply" field — a short friendly response (1 sentence)
        in the SAME LANGUAGE the user wrote in (Russian if Russian, English if English).

        Available commands:

        {"type":"rotate","angle":90,"reply":"..."}
          angle – degrees; positive = counterclockwise, negative = clockwise

        {"type":"flip","direction":"horizontal","reply":"..."}
          direction – "horizontal" | "vertical" | "both"

        {"type":"resize","width":800,"height":0,"reply":"..."}
          Set width or height to 0 to preserve aspect ratio.

        {"type":"extract_channel","channel":"red","reply":"..."}
          channel – "red" | "green" | "blue" | "hue" | "saturation" | "value"

        {"type":"detect_objects","target":"contours","reply":"..."}
          target – "contours" | "red_objects" | "green_objects" | "blue_objects" | "skin"

        {"type":"thermal","reply":"..."}
          Applies thermal camera colormap (infrared-like heat vision effect)

        {"type":"style_transfer","style":"anime","reply":"..."}
          style – "anime" | "cartoon" | "disney" | "sketch" | "oil_painting" | "watercolor"

        {"type":"grayscale","reply":"..."}

        {"type":"blur","strength":5,"reply":"..."}
          strength – integer 1–20

        {"type":"adjust","brightness":30,"contrast":1.2,"reply":"..."}
          brightness – -100…100; contrast – 0.5…3.0

        {"type":"edge_detection","threshold1":50,"threshold2":150,"reply":"..."}

        {"type":"remove_region","x":100,"y":100,"width":200,"height":200,"reply":"..."}

        If nothing matches: {"type":"unknown","message":"<reason>","reply":"<friendly explanation>"}
        """;

    public LlmService(string baseUrl, string model)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _model   = model;
        _http    = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public async Task<ImageCommand> ParseCommandAsync(string userInput, int imgW, int imgH)
    {
        var body = new
        {
            model    = _model,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = $"Image size: {imgW}x{imgH}. Command: {userInput}" }
            },
            stream      = false,
            temperature = 0.1
        };

        var content  = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{_baseUrl}/v1/chat/completions", content);
        var raw      = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"LM Studio HTTP {(int)response.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        string llmJson = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        llmJson = Regex.Replace(llmJson, @"```json\s*|```", "", RegexOptions.IgnoreCase).Trim();
        return ParseCommand(llmJson);
    }

    private static ImageCommand ParseCommand(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string typeStr = root.TryGetProperty("type", out var t) ? (t.GetString() ?? "") : "";

            var cmd = new ImageCommand
            {
                Type = typeStr.ToLowerInvariant() switch
                {
                    "rotate"          => CommandType.Rotate,
                    "flip"            => CommandType.Flip,
                    "resize"          => CommandType.Resize,
                    "extract_channel" => CommandType.ExtractChannel,
                    "detect_objects"  => CommandType.DetectObjects,
                    "blur"            => CommandType.Blur,
                    "grayscale"       => CommandType.Grayscale,
                    "style_transfer"  => CommandType.StyleTransfer,
                    "adjust"          => CommandType.Adjust,
                    "edge_detection"  => CommandType.EdgeDetection,
                    "remove_region"   => CommandType.RemoveRegion,
                    "thermal"         => CommandType.Thermal,
                    _                 => CommandType.Unknown
                }
            };

            if (root.TryGetProperty("angle",      out var v)) cmd.Angle      = v.GetDouble();
            if (root.TryGetProperty("direction",  out v))     cmd.Direction  = v.GetString();
            if (root.TryGetProperty("channel",    out v))     cmd.Channel    = v.GetString();
            if (root.TryGetProperty("target",     out v))     cmd.Target     = v.GetString();
            if (root.TryGetProperty("style",      out v))     cmd.Style      = v.GetString();
            if (root.TryGetProperty("brightness", out v))     cmd.Brightness = v.GetDouble();
            if (root.TryGetProperty("contrast",   out v))     cmd.Contrast   = v.GetDouble();
            if (root.TryGetProperty("threshold1", out v))     cmd.Threshold1 = v.GetDouble();
            if (root.TryGetProperty("threshold2", out v))     cmd.Threshold2 = v.GetDouble();
            if (root.TryGetProperty("x",          out v))     cmd.X          = v.GetInt32();
            if (root.TryGetProperty("y",          out v))     cmd.Y          = v.GetInt32();
            if (root.TryGetProperty("width",      out v))     cmd.Width      = v.GetInt32();
            if (root.TryGetProperty("height",     out v))     cmd.Height     = v.GetInt32();
            if (root.TryGetProperty("strength",   out v))     cmd.Strength   = v.GetInt32();
            if (root.TryGetProperty("message",    out v))     cmd.Message    = v.GetString();
            if (root.TryGetProperty("reply",      out v))     cmd.Reply      = v.GetString();

            return cmd;
        }
        catch (JsonException ex)
        {
            return new ImageCommand
            {
                Type    = CommandType.Unknown,
                Message = $"Некорректный JSON от модели: {ex.Message}"
            };
        }
    }
}
