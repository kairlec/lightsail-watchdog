namespace lightsail_watchdog;

using Amazon;
using Amazon.Lightsail;
using Amazon.Runtime;
using DnsUpdater;
using Notify;

public class Core
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly AWSCredentials _credentials;
    private readonly INotifyService _ns;
    private readonly IDnsUpdater _dnsUpdater;
    private readonly AmazonLightsailClient _defaultClient;

    private async Task Check()
    {
        Logger.Info("Check Start");
        var regions = await LightsailOperator.GetRegions(_defaultClient);
        var tasks = regions.Select(async region =>
        {
            var client = LightsailOperator.CreateClientWithRegion(_credentials, region.Name);
            var instances = await LightsailOperator.GetInstance(client);
            var tasks = instances.Select(async instance =>
            {
                var oldIp = instance.Instance.PublicIpAddress;
                try
                {
                    Logger.Info("Checking {0}", instance.DisplayName);
                    var result = await LightsailServerConnector.TestConnect(instance);
                    if (!result)
                    {
                        await _ns.Send($"{instance.DisplayName} test domain:[{instance.ServerName} ({instance.Instance.PublicIpAddress})] for ports[{string.Join(",", instance.ServerPort)}] Down", "Lightsail Server Update");
                        await LightsailOperator.StopInstance(client, instance.Instance);
                        try
                        {
                            await LightsailOperator.WaitInstanceState(client, instance.Instance, LightsailOperator.InstanceState.Stopped, (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
                        }
                        catch (TimeoutException)
                        {
                            throw new LightsailServerStateWaitTimeoutException($"{instance.DisplayName} wait for stop timeout[5min]");
                        }

                        await LightsailOperator.StartInstance(client, instance.Instance);
                        try
                        {
                            await LightsailOperator.WaitInstanceState(client, instance.Instance, LightsailOperator.InstanceState.Running, (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
                        }
                        catch (TimeoutException)
                        {
                            throw new LightsailServerStateWaitTimeoutException($"{instance.DisplayName} wait for start timeout[5min]");
                        }

                        var newIp = instance.Instance.PublicIpAddress;
                        while (oldIp == newIp)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5));
                            await LightsailOperator.FlushInstance(client, instance);
                            newIp = instance.Instance.PublicIpAddress;
                        }

                        if (string.IsNullOrEmpty(newIp))
                        {
                            throw new LightsailServerIpGetException($"{instance.DisplayName} get new ip failed");
                        }

                        await _dnsUpdater.UpdateDns(instance.ServerName, newIp);

                        await _ns.Send($"Update DNS {instance.ServerName} from {oldIp} to new ip {newIp}", "Lightsail Server Update");
                    }
                    else
                    {
                        Logger.Info("Checking {0} Success", instance.DisplayName);
                    }
                }
                catch (LightsailWatchDogException e)
                {
                    await _ns.Send(e.Message, "Lightsail WatchDog Error", 10);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Lightsail WatchDog Error on Checking: {0}", e.Message);
                    await _ns.Send(e.Message, "Lightsail WatchDog Error", 10, false);
                }
            });
            await Task.WhenAll(tasks);
        });

        await Task.WhenAll(tasks);
    }

    public Core(AWSCredentials credentials, INotifyService ns, IDnsUpdater dnsUpdater, TimeSpan period)
    {
        _credentials = credentials;
        _ns = ns;
        _dnsUpdater = dnsUpdater;
        _defaultClient = LightsailOperator.CreateClientWithRegion(credentials, RegionEndpoint.USWest2.SystemName);
        _ = new Timer(_ => Task.Run(Check), null, TimeSpan.Zero, period);
    }
}