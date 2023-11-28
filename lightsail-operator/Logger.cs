using NLog;

namespace lightsail_operator;

public static class LoggerInitializer
{
    public static void Initialize()
    {
        LogManager.Setup().LoadConfiguration(builder =>
        {
            builder.ForLogger().FilterMinLevel(LogLevel.Info).WriteToConsole();
            builder.ForLogger().FilterMinLevel(LogLevel.Debug).WriteToDebug();
        });
    }
}