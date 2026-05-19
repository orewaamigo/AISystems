using Microsoft.Extensions.AI;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.ComponentModel;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;

var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") 
    ?? throw new InvalidOperationException("Переменная TELEGRAM_BOT_TOKEN не установлена");

using var cts = new CancellationTokenSource();

void ValidateTelegramTokenOrThrow(string rawToken)
{
    var t = rawToken.Trim();

    if (string.Equals(t, "ТВОЙ_ТОКЕН", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("YOUR_TOKEN", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "TELEGRAM_BOT_TOKEN выглядит как заглушка ('ТВОЙ_ТОКЕН'). Укажи реальный токен из BotFather.");
    }

    // Простейшая проверка формата токена бота: <digits>:<secret>
    // (секрет обычно длинный, состоит из A-Z/a-z/0-9/_-)
    if (!Regex.IsMatch(t, @"^\d+:[A-Za-z0-9_-]{20,}$"))
    {
        throw new InvalidOperationException(
            "TELEGRAM_BOT_TOKEN не похож на токен Telegram-бота. Ожидается формат вроде '123456:ABC...'. " +
            "Скопируй токен из BotFather без лишних пробелов.");
    }
}

ValidateTelegramTokenOrThrow(token);
var bot = new TelegramBotClient(token);
User me;
try
{
    me = await bot.GetMeAsync(cts.Token);
}
catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.Message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Telegram API вернул 'Not Found' на GetMeAsync. Почти всегда это значит, что токен бота неверный/отозван.\n" +
        "Проверь, что TELEGRAM_BOT_TOKEN содержит реальный токен из BotFather (формат примерно '123456:ABC...'), " +
        "и что ты не оставил значение 'ТВОЙ_ТОКЕН'.",
        ex);
}

const string SystemPrompt =
    "Говори как Оптимус Прайм. " +
    "У тебя есть инструменты: поиск в интернете и получение текущей даты/времени — используй их по мере необходимости.";

Dictionary<long, List<ChatMessage>> conversations = new();

// Простой polling
int offset = 0;
Console.WriteLine($"@{me.Username} is running... Press Enter to terminate");
var pollTask = PollForUpdates();

Console.ReadLine();
cts.Cancel();
await pollTask;

async Task PollForUpdates()
{
    using var httpClient = new HttpClient();
    
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var updates = await bot.GetUpdatesAsync(offset, cancellationToken: cts.Token);
            
            foreach (var update in updates)
            {
                offset = update.Id + 1;
                if (update.Message?.Text is null) continue;

                var msg = update.Message;
                if (!conversations.ContainsKey(msg.Chat.Id))
                    conversations[msg.Chat.Id] = [new ChatMessage(ChatRole.System, SystemPrompt)];

                var history = conversations[msg.Chat.Id];
                history.Add(new ChatMessage(ChatRole.User, msg.Text));

                var statusMsg = await bot.SendTextMessageAsync(msg.Chat.Id, "Думаю...", cancellationToken: cts.Token);

                try
                {
                    // Запрос к Ollama через HTTP API
                    var prompt = string.Join("\n", history.Select(m => $"{m.Role}: {m.Text}"));
                    var response = await QueryOllama(httpClient, prompt, cts.Token);
                    
                    var text = response ?? "...";
                    if (!string.IsNullOrEmpty(text))
                        history.Add(new ChatMessage(ChatRole.Assistant, text));

                    await bot.EditMessageTextAsync(msg.Chat.Id, statusMsg.MessageId,
                        string.IsNullOrEmpty(text) ? "..." : text, cancellationToken: cts.Token);
                }
                catch (Exception ex)
                {
                    await bot.EditMessageTextAsync(msg.Chat.Id, statusMsg.MessageId, 
                        $"Ошибка: {ex.Message}", cancellationToken: cts.Token);
                    Console.Error.WriteLine(ex);
                }
            }

            await Task.Delay(500, cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка polling: {ex.Message}");
            await Task.Delay(1000, cts.Token);
        }
    }
}

async Task<string> QueryOllama(HttpClient client, string prompt, CancellationToken ct)
{
    var request = new { model = "gemma4:latest", prompt = prompt, stream = false };
    var jsonContent = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
    
    var response = await client.PostAsync("http://localhost:11434/api/generate", jsonContent, ct);
    response.EnsureSuccessStatusCode();
    
    var jsonResponse = await response.Content.ReadAsStringAsync(ct);
    using var doc = JsonDocument.Parse(jsonResponse);
    var root = doc.RootElement;
    
    if (root.TryGetProperty("response", out var responseProp))
        return responseProp.GetString() ?? "";
    
    return "";
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

            if (root.TryGetProperty("Answer", out var answerProp))
            {
                var answer = answerProp.GetString();
                if (!string.IsNullOrWhiteSpace(answer))
                    sb.AppendLine(answer);
            }

            if (root.TryGetProperty("AbstractText", out var abstractProp))
            {
                var abstractText = abstractProp.GetString();
                if (!string.IsNullOrWhiteSpace(abstractText))
                    sb.AppendLine(abstractText);
            }

            if (sb.Length == 0 && root.TryGetProperty("RelatedTopics", out var topicsProp))
            {
                foreach (var topic in topicsProp.EnumerateArray().Take(5))
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
