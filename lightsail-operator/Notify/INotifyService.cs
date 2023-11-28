namespace lightsail_watchdog.Notify;

public interface INotifyService
{
    Task Send(string message, string title, int priority = 5, bool log = true, CancellationToken cancellationToken = default);
}