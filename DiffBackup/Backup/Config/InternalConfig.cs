using System;

namespace DiffBackup.Backup.Config
{
    public class InternalConfig : BaseConfig
    {
        public TimeSpanConfig ThrottleTimeSpan { get; set; } = TimeSpan.FromSeconds(60);

        public TimeSpanConfig BackupTaskTimeoutTimeSpan { get; set; } = TimeSpan.FromMinutes(3);

        public WorldSaveTrackingStrategy WorldSaveTrackingStrategy { get; set; } =
            WorldSaveTrackingStrategy.SaveEventListener;
    }
}