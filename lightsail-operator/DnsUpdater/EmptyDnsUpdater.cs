namespace lightsail_watchdog.DnsUpdater;

public class EmptyDnsUpdater : IDnsUpdater
{
    public Task UpdateDns(string name, string ip)
    {
        return Task.CompletedTask;
    }
}