using Yawss.Core.Config;

namespace Yawss.Server.Hosting;

public static class LoggingSetup
{
    public static void Configure(ILoggingBuilder logging, LoggingConfig config)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(config);

        logging.ClearProviders();
        logging.SetMinimumLevel(Map(config.Level));
        logging.AddFilter("Microsoft.AspNetCore", Microsoft.Extensions.Logging.LogLevel.Warning);
        logging.AddFilter("Microsoft.Hosting.Lifetime", Microsoft.Extensions.Logging.LogLevel.Information);

        if (config.Format == LogFormat.Json)
        {
            logging.AddJsonConsole(o => o.UseUtcTimestamp = true);
        }
        else
        {
            logging.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
        }
    }

    private static Microsoft.Extensions.Logging.LogLevel Map(Yawss.Core.Config.LogLevel level) => level switch
    {
        Yawss.Core.Config.LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
        Yawss.Core.Config.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
        Yawss.Core.Config.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
        Yawss.Core.Config.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
        Yawss.Core.Config.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
        Yawss.Core.Config.LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
        _ => Microsoft.Extensions.Logging.LogLevel.Information,
    };
}
