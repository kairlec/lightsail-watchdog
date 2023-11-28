namespace lightsail_watchdog.Notify;

public class EmptyNotifyService : INotifyService
{
    public Task Send(string message, string title, int priority = 5, bool log = true, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}