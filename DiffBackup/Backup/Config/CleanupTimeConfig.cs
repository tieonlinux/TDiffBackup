using System;

namespace DiffBackup.Backup.Config
{
    public class CleanupTimeConfig : BaseConfig
    {
        public CleanupTimeConfig()
        {
        }

        public CleanupTimeConfig(string name, TimeSpan ageFilter, TimeSpan keepBackupEveryTimeSpan)
        {
            Name = name;
            AgeFilter = ageFilter;
            KeepBackupEveryTimeSpan = keepBackupEveryTimeSpan;
        }

        public CleanupTimeConfig(CleanupTimeConfig copy) : base(copy)
        {
        }

        public string Name { get; set; } = "";

        public TimeSpanConfig AgeFilter { get; set; } = TimeSpan.Zero;

        public TimeSpanConfig KeepBackupEveryTimeSpan { get; set; } = TimeSpan.MaxValue;
    }
}