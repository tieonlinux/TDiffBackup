using System.Diagnostics;
using TerrariaApi.Server;

namespace DiffBackup.Logger
{
    // ReSharper disable once InconsistentNaming
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
}