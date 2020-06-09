using System.Diagnostics;
using TerrariaApi.Server;

namespace DiffBackup
{
    public interface ITlog
    {
        void LogWriteLine(string text, TraceLevel level);
        void LogDebug(string text, TraceLevel level = TraceLevel.Info);
        void LogInfo(string text);
        void LogVerbose(string text);
        void LogWarn(string text);
        void LogError(string text);
    }

    public abstract class DefaultTlog : ITlog
    {
        public abstract void LogWriteLine(string text, TraceLevel level);

        public void LogDebug(string text, TraceLevel level)
        {
#if DEBUG
            LogWriteLine(text, level);
#endif
        }

        public void LogInfo(string text) => LogWriteLine(text, TraceLevel.Info);

        public void LogVerbose(string text) => LogWriteLine(text, TraceLevel.Verbose);
        public void LogWarn(string text) => LogWriteLine(text, TraceLevel.Warning);

        public void LogError(string text) => LogWriteLine(text, TraceLevel.Error);
    }

    public class TLog : DefaultTlog
    {
        private readonly TerrariaPlugin _plugin;

        public TLog(TerrariaPlugin plugin)
        {
            _plugin = plugin;
        }

        public override void LogWriteLine(string text, TraceLevel level)
        {
            ServerApi.LogWriter.PluginWriteLine(_plugin, text, level);
        }
    }

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


        public static void LogInfo(this TerrariaPlugin plugin, string text) => Logger(plugin).LogInfo(text);
        public static void LogVerbose(this TerrariaPlugin plugin, string text) => Logger(plugin).LogVerbose(text);
        public static void LogWarn(this TerrariaPlugin plugin, string text) => Logger(plugin).LogWarn(text);
        public static void LogError(this TerrariaPlugin plugin, string text) => Logger(plugin).LogError(text);
    }
}