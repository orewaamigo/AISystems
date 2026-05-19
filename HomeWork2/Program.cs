using Microsoft.Extensions.AI;
using OllamaSharp;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.ComponentModel;
using System.Text.Json;
using System.Globalization;

using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient("8549128658:AAGYVqMpgV6ziJ4s1ivRPcFDNRNay1VhFZU", cancellationToken: cts.Token);
var me = await bot.GetMe();

IChatClient chatClient = ((IChatClient)new OllamaApiClient(new Uri("http://localhost:11434")))
    .AsBuilder()
    .Build();

var chatOptions = new ChatOptions
{
    ModelId = "gemma4:latest",
};

const string SystemPrompt =
    "Говори как Оптимус Прайм. " +
    "У тебя есть инструменты: поиск в интернете и получение текущей даты/времени — используй их по мере необходимости.";

Dictionary<long, List<ChatMessage>> conversations = new();

bot.OnMessage += OnMessage;
Console.WriteLine($"@{me.Username} is running... Press Enter to terminate");
Console.ReadLine();
cts.Cancel();

async Task OnMessage(Message msg, UpdateType type)
{
    if (msg.Text is null) return;

    if (!conversations.ContainsKey(msg.Chat.Id))
        conversations[msg.Chat.Id] = [new ChatMessage(ChatRole.System, SystemPrompt)];

    var history = conversations[msg.Chat.Id];
    history.Add(new ChatMessage(ChatRole.User, msg.Text));

    var statusMsg = await bot.SendMessage(msg.Chat.Id, "Думаю...");

    try
    {
        var response = await chatClient.GetResponseAsync(history, chatOptions, cts.Token);
        history.AddMessages(response);

        var text = response.Text;
        await bot.EditMessageText(msg.Chat.Id, statusMsg.Id,
            string.IsNullOrEmpty(text) ? "..." : text);
    }
    catch (Exception ex)
    {
        await bot.EditMessageText(msg.Chat.Id, statusMsg.Id, $"Ошибка: {ex.Message}");
        Console.Error.WriteLine(ex);
    }
}

static class BotTools
{
    [Description("Ищет информацию в интернете по запросу пользователя")]
    public static async Task<string> WebSearch(
        [Description("Поисковый запрос")] string query)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OptimusPrimeBot/1.0");
        try
        {
            var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";
            var json = await http.GetStringAsync(url);
            var root = JsonDocument.Parse(json).RootElement;

            var sb = new System.Text.StringBuilder();

            var answer = root.GetProperty("Answer").GetString();
            if (!string.IsNullOrWhiteSpace(answer))
                sb.AppendLine(answer);

            var abstractText = root.GetProperty("AbstractText").GetString();
            if (!string.IsNullOrWhiteSpace(abstractText))
                sb.AppendLine(abstractText);

            if (sb.Length == 0)
            {
                foreach (var topic in root.GetProperty("RelatedTopics").EnumerateArray().Take(5))
                {
                    if (topic.TryGetProperty("Text", out var t) && !string.IsNullOrWhiteSpace(t.GetString()))
                        sb.AppendLine($"• {t.GetString()}");
                }
            }

            return sb.Length > 0 ? sb.ToString().Trim() : "По данному запросу ничего не найдено.";
        }
        catch (Exception ex)
        {
            return $"Ошибка при поиске: {ex.Message}";
        }
    }

    [Description("Возвращает текущую дату и время")]
    public static string GetCurrentDateTime() =>
        DateTime.Now.ToString("dddd, dd MMMM yyyy, HH:mm:ss", new CultureInfo("ru-RU"));
}
