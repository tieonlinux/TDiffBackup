using System;

namespace DiffBackup.Backup.Config
{
    public class StrategyConfig : BaseConfig
    {
        public TimeSpanConfig EveryXTimeSpanDoFullBackup { get; set; } = TimeSpan.FromDays(7);

        public double DiffFillFactorToStartFullBackup { get; set; } = 0.5;
        public CleanupConfig Cleanup { get; set; } = new CleanupConfig();
    }
}