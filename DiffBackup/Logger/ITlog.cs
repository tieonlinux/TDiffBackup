using System.Diagnostics;

namespace DiffBackup.Logger
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
}