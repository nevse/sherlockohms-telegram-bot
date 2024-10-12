using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

class Program
{
    private static TelegramBotClient? botClient;

    static async Task Main(string[] args)
    {
        string? token = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Please set TELEGRAM_TOKEN environment variable");
            return;
        }
        string? telegramServerUrl = Environment.GetEnvironmentVariable("TELEGRAM_SERVER");
        if (string.IsNullOrEmpty(telegramServerUrl))
        {
            Console.WriteLine("Please set TELEGRAM_SERVER environment variable");
            return;
        }

        var options = new TelegramBotClientOptions(token, telegramServerUrl);
        botClient = new TelegramBotClient(options);

        var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Receive all update types
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Start listening for @{me.Username}");
        await Task.Delay(Timeout.Infinite, cts.Token);

        // Send cancellation request to stop bot
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {        
        if (update.Message is not { } message)
            return;

        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        if (messageText.StartsWith("/start"))
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Привет! Я простой чат-бот. Напиши мне что-нибудь.",
                cancellationToken: cancellationToken
            );
        }
        else if (messageText.StartsWith("/help"))
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Я могу скачивать видео с YouTube. Просто отправь мне ссылку на видео.",
                cancellationToken: cancellationToken
            );
        }
        else if (IsYoutubeLink(messageText))
        {
            var url = messageText.Trim();
            Console.WriteLine($"Downloading video from {url} from {update.Message.Chat.Username}");
            await DownloadYoutubeVideoAsync(chatId, url, cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Ты написал: {messageText}",
                cancellationToken: cancellationToken
            );
        }
    }

    static bool IsYoutubeLink(string url)
    {
        return url.Contains("youtube.com") || url.Contains("youtu.be");
    }

    static async Task DownloadYoutubeVideoAsync(long chatId, string url, CancellationToken cancellationToken)
    {
        if (botClient == null) {
            Console.WriteLine("Bot client is not initialized");
            return;
        }

        try
        {
            var youtube = new YoutubeClient();
            var video = await youtube.Videos.GetAsync(url, cancellationToken);
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id, cancellationToken);
            //download audio
            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            if (streamInfo != null)
            {
                string fileName = GetEscapedFileName(video.Title);
                var filePath = Path.Combine(Path.GetTempPath(), fileName + ".mp3");
                await youtube.Videos.Streams.DownloadAsync(streamInfo, filePath, cancellationToken: cancellationToken);

                using (var stream = System.IO.File.OpenRead(filePath))
                {
                    await botClient.SendAudioAsync(
                        chatId: chatId,
                        InputFile.FromStream(stream, video.Title + ".mp3"),
                        caption: "Вот ваш видеофайл",
                        cancellationToken: cancellationToken
                    );
                }

                System.IO.File.Delete(filePath);
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Не удалось найти поток для этого видео.",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Произошла ошибка при загрузке видео: " + ex.Message,
                cancellationToken: cancellationToken
            );
        }
    }

    private static string GetEscapedFileName(string title)
    {
        return string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}