using System;
using System.Collections.Generic;

namespace DiffBackup.Backup.Config
{
    public class CleanupConfig : BaseConfig
    {
        public TimeSpanConfig ScheduleTimeSpan { get; set; } = TimeSpan.FromHours(12);

        public List<CleanupTimeConfig> WldFiles { get; set; } = new List<CleanupTimeConfig>
        {
            new CleanupTimeConfig("yearly", TimeSpan.FromDays(365), TimeSpan.FromDays(3)),
            new CleanupTimeConfig("monthly", TimeSpan.FromDays(30), TimeSpan.FromHours(12)),
            new CleanupTimeConfig("weekly", TimeSpan.FromDays(7), TimeSpan.FromHours(1))
        };

        public List<CleanupTimeConfig> DiffFiles { get; set; } = new List<CleanupTimeConfig>
        {
            new CleanupTimeConfig("yearly", TimeSpan.FromDays(365), TimeSpan.FromDays(365)),
            new CleanupTimeConfig("monthly", TimeSpan.FromDays(30), TimeSpan.FromMinutes(30)),
            new CleanupTimeConfig("weekly", TimeSpan.FromDays(7), TimeSpan.FromMinutes(1))
        };
    }
}