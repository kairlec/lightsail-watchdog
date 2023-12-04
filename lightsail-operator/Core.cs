namespace lightsail_watchdog;

using Amazon;
using Amazon.Lightsail;
using Amazon.Runtime;
using DnsUpdater;
using Notify;

public class Core(AWSCredentials credentials, INotifyService ns, IDnsUpdater dnsUpdater) : IDisposable
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly AmazonLightsailClient _defaultClient = LightsailOperator.CreateClientWithRegion(credentials, RegionEndpoint.USWest2.SystemName);
    private PeriodicTimer? _timer;
    private Task? _timerTask;

    private async Task Check()
    {
        try
        {
            Logger.Info("Check Start");
            var regions = await LightsailOperator.GetRegions(_defaultClient);
            Logger.Info("Regions: {0}", string.Join(",", regions.Select(r => r.Name)));
            var tasks = regions.Select(async region =>
            {
                var client = LightsailOperator.CreateClientWithRegion(credentials, region.Name);
                var instances = await LightsailOperator.GetInstance(client);
                Logger.Info("Region [{0}] Instances: {1}", region, string.Join(",", instances.Select(i => i.DisplayName)));
                var tasks = instances.Select(async instance =>
                {
                    var oldIp = instance.Instance.PublicIpAddress;
                    try
                    {
                        Logger.Info("Checking {0}", instance.DisplayName);
                        var result = await LightsailServerConnector.TestConnect(instance);
                        if (!result)
                        {
                            await ns.Send($"{instance.DisplayName} test domain:[{instance.ServerName} ({instance.Instance.PublicIpAddress})] for ports[{string.Join(",", instance.ServerPort)}] Down", "Lightsail Server Update");
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

                            await dnsUpdater.UpdateDns(instance.ServerName, newIp);

                            await ns.Send($"Update DNS {instance.ServerName} from {oldIp} to new ip {newIp}", "Lightsail Server Update");
                        }
                        else
                        {
                            Logger.Info("Checking {0} Success", instance.DisplayName);
                        }
                    }
                    catch (LightsailWatchDogException e)
                    {
                        await ns.Send(e.Message, "Lightsail WatchDog Error", 10);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Lightsail WatchDog Error on Checking: {0}", e.Message);
                        await ns.Send(e.Message, "Lightsail WatchDog Error", 10, false);
                    }
                });
                await Task.WhenAll(tasks);
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Lightsail WatchDog Error on Checking: {0}", e.Message);
            await ns.Send(e.Message, "Lightsail WatchDog Error", 10, false);
        }
    }

    public void Start(TimeSpan period)
    {
        _timer?.Dispose();
        _timer = new PeriodicTimer(period);
        _timerTask = Task.Run(async () =>
        {
            Logger.Info("Lightsail WatchDog Start Checking every {0}", period);
            while (await _timer.WaitForNextTickAsync())
            {
                try
                {
                    await Check();
                }
                catch (Exception e)
                {
                    Logger.Fatal(e, "Lightsail WatchDog Error on Checking: {0}", e.Message);
                    await ns.Send(e.Message, "Lightsail WatchDog Fatal Error", 10, false);
                }
            }

            Logger.Info("Lightsail WatchDog Stop Checking");
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing) return;
        if (_timer == null) return;
        _timerTask?.Dispose();
        _timer.Dispose();
        _timer = null;
    }
}