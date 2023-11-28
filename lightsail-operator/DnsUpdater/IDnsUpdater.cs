namespace lightsail_watchdog.DnsUpdater;

public interface IDnsUpdater
{
    public Task UpdateDns(string name, string ip);
}