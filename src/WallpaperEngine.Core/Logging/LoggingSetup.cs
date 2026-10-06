using Serilog;
using WallpaperEngine.Core.Paths;

namespace WallpaperEngine.Core.Logging;

public static class LoggingSetup
{
    public static void Init()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppPaths.Logs, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();
    }

    public static void Shutdown() => Log.CloseAndFlush();
}