using System.Diagnostics;
using TerrariaApi.Server;

namespace DiffBackup.Logger
{
    public static class LoggingExt
    {
        public static ITlog Logger(this TerrariaPlugin plugin)
        {
            return new TLog(plugin);
        }

        public static void LogDebug(this TerrariaPlugin plugin, string text, TraceLevel level)
        {
#if DEBUG
            Logger(plugin).LogDebug(text, level);
#endif
        }

        public static void LogDebug(this TerrariaPlugin plugin, string text)
        {
#if DEBUG
            Logger(plugin).LogDebug(text);
#endif
        }


        public static void LogInfo(this TerrariaPlugin plugin, string text)
        {
            Logger(plugin).LogInfo(text);
        }

        public static void LogVerbose(this TerrariaPlugin plugin, string text)
        {
            Logger(plugin).LogVerbose(text);
        }

        public static void LogWarn(this TerrariaPlugin plugin, string text)
        {
            Logger(plugin).LogWarn(text);
        }

        public static void LogError(this TerrariaPlugin plugin, string text)
        {
            Logger(plugin).LogError(text);
        }
    }
}