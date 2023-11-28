namespace lightsail_operator;

public abstract class LightsailWatchDogException(string message) : Exception(message);

public abstract class CloudFlareException(string message) : LightsailWatchDogException(message);

public abstract class LightsailServerException(string message) : LightsailWatchDogException(message);

public class CloudFlareDnsRecordGetException(string message) : CloudFlareException(message);

public class CloudFlareDnsRecordUpdateException(string message) : CloudFlareException(message);

public class CloudFlareDnsRecordAddException(string message) : CloudFlareException(message);

public class LightsailServerIpGetException(string message) : LightsailServerException(message);

public class LightsailServerStateWaitTimeoutException(string message) : LightsailServerException(message);