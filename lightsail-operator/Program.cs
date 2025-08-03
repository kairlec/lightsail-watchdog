using System.Net;
using Amazon.Runtime;
using lightsail_watchdog;
using lightsail_watchdog.DnsUpdater;
using lightsail_watchdog.Notify;

// disable server cert validate
ServicePointManager.ServerCertificateValidationCallback += (_, _, _, _) => true;

LoggerInitializer.Initialize();

var awsAccessKeyId = EnvironmentHelper.GetStringRequire("AWS_ACCESS_KEY_ID");
var awsAccessSecretKeyId = EnvironmentHelper.GetStringRequire("AWS_SECRET_ACCESS_KEY");
var gotifyUrl = EnvironmentHelper.GetString("GOTIFY_URL");
var gotifyToken = EnvironmentHelper.GetString("GOTIFY_TOKEN");
var cloudFlareEmail = EnvironmentHelper.GetString("CLOUDFLARE_EMAIL");
var cloudFlareToken = EnvironmentHelper.GetString("CLOUDFLARE_TOKEN");
var cloudFlareZoneId = EnvironmentHelper.GetString("CLOUDFLARE_ZONE_ID");
var checkPeriod = EnvironmentHelper.GetInt("CHECK_PERIOD_MINUTES") ?? 60;
var useRegionsStr = EnvironmentHelper.GetString("USE_REGIONS");

var useRegions = string.IsNullOrWhiteSpace(useRegionsStr)
    ? null
    : useRegionsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var credentials = new BasicAWSCredentials(awsAccessKeyId, awsAccessSecretKeyId);

INotifyService ns;
if (
    !string.IsNullOrWhiteSpace(gotifyUrl) &&
    !string.IsNullOrWhiteSpace(gotifyToken))
{
    ns = new GotifyService(gotifyUrl, gotifyToken);
}
else
{
    ns = new EmptyNotifyService();
}

IDnsUpdater dnsUpdater;
if (
    !string.IsNullOrWhiteSpace(cloudFlareEmail) &&
    !string.IsNullOrWhiteSpace(cloudFlareToken) &&
    !string.IsNullOrWhiteSpace(cloudFlareZoneId))
{
    dnsUpdater = new CloudFlareDnsUpdater(cloudFlareEmail, cloudFlareToken, cloudFlareZoneId);
}
else
{
    dnsUpdater = new EmptyDnsUpdater();
}

var core = new Core(credentials, ns, dnsUpdater, useRegions);

core.Start(TimeSpan.FromMinutes(checkPeriod));

var quitEvent = new ManualResetEvent(false);

Console.CancelKeyPress += (_, eArgs) =>
{
    quitEvent.Set();
    eArgs.Cancel = true;
};

quitEvent.WaitOne();