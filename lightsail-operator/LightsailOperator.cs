using System.Diagnostics;
using Amazon;
using Amazon.Lightsail.Model;

namespace lightsail_operator;

using System;
using Amazon.Lightsail;
using Amazon.Runtime;

public class WrappedInstance(Instance instance, string serverName, IEnumerable<int> serverPort, string displayName)
{
    public Instance Instance { internal set; get; } = instance;
    public string ServerName { get; } = serverName;
    public IEnumerable<int> ServerPort { get; } = serverPort;
    public string DisplayName { get; } = displayName;
}

public static class LightsailOperator
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public static async Task<List<Region>> GetRegions(AmazonLightsailClient client)
    {
        var request = new GetRegionsRequest
        {
            IncludeAvailabilityZones = true
        };

        var resp = await client.GetRegionsAsync(request);
        return resp.Regions;
    }

    public static AmazonLightsailClient CreateClientWithRegion(AWSCredentials credentials, string region)
    {
        return new AmazonLightsailClient(credentials, new AmazonLightsailConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        });
    }

    public static async Task<List<WrappedInstance>> GetInstance(AmazonLightsailClient client)
    {
        var request = new GetInstancesRequest();
        var resp = await client.GetInstancesAsync(request);

        return resp.Instances.Select(WrappedInstance).Where(x => x != null).Select(x => x!).ToList();
    }

    public static async Task FlushInstance(AmazonLightsailClient client, WrappedInstance instance)
    {
        var request = new GetInstanceRequest
        {
            InstanceName = instance.Instance.Name
        };
        var resp = await client.GetInstanceAsync(request);

        instance.Instance = resp.Instance;
    }

    private static WrappedInstance? WrappedInstance(Instance instance)
    {
        try
        {
            List<int>? serverPort = null;
            string? serverName = null;
            string? displayName = null;
            instance.Tags.ForEach(tag =>
            {
                switch (tag.Key)
                {
                    case "server_port":
                        serverPort = tag.Value.Split('/').Select(int.Parse).ToList();
                        break;
                    case "cf_domain":
                        serverName = tag.Value;
                        break;
                    case "display_name":
                        displayName = tag.Value;
                        break;
                }
            });
            if (serverName != null && serverPort != null)
            {
                return new WrappedInstance(instance, serverName, serverPort, displayName ?? serverName);
            }

            return null;
        }
        catch (Exception e)
        {
            Logger.Warn(e, $"WrappedInstance Error: {e.Message}");
            return null;
        }
    }

    public static async Task StopInstance(AmazonLightsailClient client, Instance instance)
    {
        var request = new StopInstanceRequest
        {
            InstanceName = instance.Name,
            Force = true
        };
        await client.StopInstanceAsync(request);
    }

    public static async Task StartInstance(AmazonLightsailClient client, Instance instance)
    {
        var request = new StartInstanceRequest
        {
            InstanceName = instance.Name
        };
        await client.StartInstanceAsync(request);
    }

    public enum InstanceState
    {
        Pending = 0,
        Running = 16,
        ShuttingDown = 32,
        Terminated = 48,
        Stopping = 64,
        Stopped = 80,
    }

    public static async Task WaitInstanceState(AmazonLightsailClient client, Instance instance, InstanceState state, int timeoutMills = Timeout.Infinite, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var request = new GetInstanceStateRequest
            {
                InstanceName = instance.Name
            };
            while (true)
            {
                var resp = await client.GetInstanceStateAsync(request, cancellationToken);

                Logger.Debug($"Waiting for instance {instance.Name} to be {state}, current state is {resp.State.Code}, timeout({timeoutMills}) elapsed {watch.ElapsedMilliseconds}ms");
                if (resp.State.Code == (int)state)
                    return;
                if (timeoutMills == Timeout.Infinite)
                {
                    await Task.Delay(1000, cancellationToken);
                }
                else
                {
                    if (watch.ElapsedMilliseconds > timeoutMills)
                    {
                        throw new TimeoutException($"Timeout waiting for instance {instance.Name} to be {state} for {timeoutMills}ms");
                    }

                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        finally
        {
            watch.Stop();
        }
    }
}