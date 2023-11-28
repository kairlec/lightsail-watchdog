using System.Text;
using System.Text.Json;

namespace lightsail_watchdog;

public interface INotifyService
{
    Task Send(string message, string title, int priority = 5, bool log = true, CancellationToken cancellationToken = default);
}

public class EmptyNotifyService : INotifyService
{
    public Task Send(string message, string title, int priority = 5, bool log = true, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class GotifyService(string serverUrl, string token) : INotifyService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly HttpClient _client = new();
    private readonly string _url = $"{serverUrl}/message?token={token}";

    public async Task Send(string message, string title, int priority = 5, bool log = true, CancellationToken cancellationToken = default)
    {
        if (priority == 5 && log)
        {
            Logger.Info(message);
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _url)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    message,
                    title,
                    priority
                }), Encoding.UTF8, "application/json")
            };
            await _client.SendAsync(request, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Gotify Send Error: {e.Message}");
        }
    }
}