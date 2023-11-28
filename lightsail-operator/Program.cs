using Amazon.Runtime;
using lightsail_operator;

LoggerInitializer.Initialize();

var awsAccessKeyId = EnvironmentHelper.GetStringRequire("AWS_ACCESS_KEY_ID");
var awsAccessSecretKeyId = EnvironmentHelper.GetStringRequire("AWS_SECRET_ACCESS_KEY");
var gotifyUrl = EnvironmentHelper.GetString("GOTIFY_URL");
var gotifyToken = EnvironmentHelper.GetString("GOTIFY_TOKEN");
var cloudFlareEmail = EnvironmentHelper.GetStringRequire("CLOUDFLARE_EMAIL");
var cloudFlareToken = EnvironmentHelper.GetStringRequire("CLOUDFLARE_TOKEN");
var cloudFlareZoneId = EnvironmentHelper.GetStringRequire("CLOUDFLARE_ZONE_ID");
var checkPeriod = EnvironmentHelper.GetInt("CHECK_PERIOD_MINUTES") ?? 60;

var credentials = new BasicAWSCredentials(awsAccessKeyId, awsAccessSecretKeyId);

INotifyService ns;
if (!string.IsNullOrWhiteSpace(gotifyUrl) && !string.IsNullOrWhiteSpace(gotifyToken))
{
    ns = new GotifyService(gotifyUrl, gotifyToken);
}
else
{
    ns = new EmptyNotifyService();
}

var cloudFlareDnsUpdater = new CloudFlareDnsUpdater(cloudFlareEmail, cloudFlareToken, cloudFlareZoneId);

_ = new Core(credentials, ns, cloudFlareDnsUpdater, TimeSpan.FromMinutes(checkPeriod));

var quitEvent = new ManualResetEvent(false);

Console.CancelKeyPress += (_, eArgs) =>
{
    quitEvent.Set();
    eArgs.Cancel = true;
};

quitEvent.WaitOne();