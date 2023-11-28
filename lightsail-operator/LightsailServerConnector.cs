namespace lightsail_operator;

public static class LightsailServerConnector
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private static readonly HttpClient HttpClient = new();

    public static async Task<bool> TestConnect(WrappedInstance instance)
    {
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var results = instance.ServerPort.Select(async port =>
        {
            var url = $"https://{instance.ServerName}:{port}/";
            var retryCount = 3;
            while (retryCount-- > 0)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                try
                {
                    await HttpClient.SendAsync(request, cancellationTokenSource.Token);
                    return true;
                }
                catch (Exception e)
                {
                    if (e is OperationCanceledException)
                    {
                        return false;
                    }

                    Logger.Debug($"{instance.Instance.Name}({instance.Instance.Arn}) test url:[{url} ({instance.Instance.PublicIpAddress}:{port})] failed {e.Message}, retrying({retryCount})");
                }
            }

            return false;
        });
        var result = await results.LogicalAny();
        await cancellationTokenSource.CancelAsync();
        return result;
    }

    private static async Task<bool> LogicalAny(this IEnumerable<Task<bool>> tasks)
    {
        var remainingTasks = new HashSet<Task<bool>>(tasks);
        while (remainingTasks.Count != 0)
        {
            var next = await Task.WhenAny(remainingTasks);
            if (next.Result)
                return true;
            remainingTasks.Remove(next);
        }

        return false;
    }
}