using System.Diagnostics;

namespace DiffBackup.Logger
{
    public abstract class BaseTlog : ITlog
    {
        public abstract void LogWriteLine(string text, TraceLevel level);

        public void LogDebug(string text, TraceLevel level)
        {
#if DEBUG
            LogWriteLine(text, level);
#endif
        }

        public void LogInfo(string text)
        {
            LogWriteLine(text, TraceLevel.Info);
        }

        public void LogVerbose(string text)
        {
            LogWriteLine(text, TraceLevel.Verbose);
        }

        public void LogWarn(string text)
        {
            LogWriteLine(text, TraceLevel.Warning);
        }

        public void LogError(string text)
        {
            LogWriteLine(text, TraceLevel.Error);
        }
    }
}