using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DiffBackup.Logger;

#nullable enable
namespace DiffBackup.Backup
{
    public class DefaultBackupStrategy : IBackupStrategy
    {
        public readonly ITlog Log;

        public DefaultBackupStrategy(ITlog log)
        {
            Log = log;
            var env = Environment.GetEnvironmentVariables();

            // ReSharper disable once InconsistentNaming
            const string TdiffOverwriteForceFullBackupTimeSpan = nameof(TdiffOverwriteForceFullBackupTimeSpan);
            if (env.Contains(TdiffOverwriteForceFullBackupTimeSpan))
            {
                ForceFullBackupTimeSpan =
                    TimeSpan.FromDays(
                        double.Parse(Environment.GetEnvironmentVariable(TdiffOverwriteForceFullBackupTimeSpan)));
            }

            // ReSharper disable once InconsistentNaming
            const string TdiffOverwriteFillFactor = nameof(TdiffOverwriteFillFactor);
            if (env.Contains(TdiffOverwriteFillFactor))
            {
                FillFactor = float.Parse(Environment.GetEnvironmentVariable(TdiffOverwriteFillFactor));
            }
        }

        public TimeSpan ForceFullBackupTimeSpan { get; set; } = TimeSpan.FromDays(7);
        public float FillFactor { get; set; } = 0.5f;

        public BackupRepositoryEntry? GetReference(BackupRepository repo, DateTime date)
        {
            var backups = BackupUtils.ListBackup(repo).OrderBy(backup => backup.DateTime - date).ToArray();
            // ReSharper disable once UseDeconstruction
            var reference = backups.Select((backup, index) => (Backup: backup, Index: index))
                .FirstOrDefault(t => !t.Backup.IsDiff && t.Backup.DateTime <= date);
            if (reference.Backup is null)
            {
                return null;
            }

            if (ForceFullBackupTimeSpan > TimeSpan.Zero &&
                reference.Backup.DateTime < date - ForceFullBackupTimeSpan)
            {
                return null;
            }

            var diffs = backups.Select((backup, index) => (Backup: backup, Index: index)).Where(t =>
                t.Index > reference.Index && t.Backup.DateTime <= date && t.Backup.IsDiff);

            var size = diffs.Sum(t => t.Backup.Entry.Length);
            return FillFactor > 0 && size > FillFactor * reference.Backup.Entry.Length ? null : reference.Backup.Entry;
        }

        public IList<BackupRepositoryEntry> ListExpiredEntries(BackupRepository repo, DateTime date)
        {
            var backups = BackupUtils.ListBackup(repo, true).ToArray();
            var markedForDeletion = new HashSet<BackupUtils.BackupEntry>();

            IEnumerable<BackupUtils.BackupEntry> MarkOldEntries(IEnumerable<BackupUtils.BackupEntry> backups,
                TimeSpan age, TimeSpan keepEvery)
            {
                Debug.Assert(keepEvery > TimeSpan.Zero);
                var oldEntries = backups.Where(backup => date - backup.DateTime > age)
                    .Where(backup => !markedForDeletion.Contains(backup))
                    .OrderBy(backup => backup.DateTime)
                    .ToArray();
                if (!oldEntries.Any())
                {
                    yield break;
                }

                var i = 0;
                var prevBackup = oldEntries.First();
                while (++i < oldEntries.Length)
                {
                    var backup = oldEntries[i];
                    if (backup.DateTime - prevBackup.DateTime <= keepEvery)
                    {
                        if (markedForDeletion.Add(backup))
                        {
                            yield return backup;
                        }
                    }
                    else
                    {
                        prevBackup = backup;
                    }
                }
            }

            CleanupTimeConfig[] worldCleanupTimeConfigs =
            {
                new CleanupTimeConfig("yearly", TimeSpan.FromDays(365.25), TimeSpan.FromDays(3)),
                new CleanupTimeConfig("monthly", TimeSpan.FromDays(30.44), TimeSpan.FromHours(12)),
                new CleanupTimeConfig("weekly", TimeSpan.FromDays(7), TimeSpan.FromHours(1))
            };

            CleanupTimeConfig[] diffCleanupTimeConfigs =
            {
                new CleanupTimeConfig("yearly", TimeSpan.FromDays(365.25), TimeSpan.MaxValue),
                new CleanupTimeConfig("monthly", TimeSpan.FromDays(30.44), TimeSpan.FromMinutes(30)),
                new CleanupTimeConfig("weekly", TimeSpan.FromDays(7), TimeSpan.FromMinutes(1))
            };


            foreach (var config in worldCleanupTimeConfigs)
            {
                var marked = ConsumeIEnumerable(MarkOldEntries(backups.Where(backup => !backup.IsDiff),
                    config.AgeFilter,
                    config.KeepBackupEveryTimeSpan));
                Log.LogDebug($"Marked {marked} entries for {config.Text.ToLower()} wld cleanup");
            }

            foreach (var config in diffCleanupTimeConfigs)
            {
                var marked = ConsumeIEnumerable(MarkOldEntries(backups.Where(backup => backup.IsDiff), config.AgeFilter,
                    config.KeepBackupEveryTimeSpan));
                Log.LogDebug($"Marked {marked} entries for {config.Text.ToLower()} diff cleanup");
            }

            foreach (var entry in backups.Where(backup => !backup.Valid))
            {
                markedForDeletion.Add(entry);
            }

            return markedForDeletion.Select(entry => entry.Entry).ToList();
        }

        public void Dispose()
        {
        }

        internal static int ConsumeIEnumerable<T>(IEnumerable<T> enumerable)
        {
            return enumerable.Count();
        }

        public readonly struct CleanupTimeConfig
        {
            public readonly string Text;
            public readonly TimeSpan AgeFilter;
            public readonly TimeSpan KeepBackupEveryTimeSpan;

            public CleanupTimeConfig(string text, TimeSpan ageFilter, TimeSpan keepBackupEveryTimeSpan)
            {
                Text = text;
                AgeFilter = ageFilter;
                KeepBackupEveryTimeSpan = keepBackupEveryTimeSpan;
            }
        }
    }
}