using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DiffBackup.Backup.Config;
using DiffBackup.Logger;

#nullable enable
namespace DiffBackup.Backup
{
    public class DefaultBackupStrategy : IBackupStrategy
    {
        public readonly StrategyConfig Config;
        public readonly ITlog Log;

        public DefaultBackupStrategy(ITlog log, StrategyConfig config)
        {
            Log = log;
            Config = config.Clone<StrategyConfig>();
            Config.SetFromJson(config.ToJson());
            var env = Environment.GetEnvironmentVariables();

            // ReSharper disable once InconsistentNaming
            const string TdiffOverwriteForceFullBackupTimeSpan = nameof(TdiffOverwriteForceFullBackupTimeSpan);
            if (env.Contains(TdiffOverwriteForceFullBackupTimeSpan))
            {
                ForceFullBackupTimeSpan =
                    TimeSpan.FromDays(
                        double.Parse(Environment.GetEnvironmentVariable(TdiffOverwriteForceFullBackupTimeSpan)));
            }
            else
            {
                ForceFullBackupTimeSpan = config.EveryXTimeSpanDoFullBackup;
            }

            // ReSharper disable once InconsistentNaming
            const string TdiffOverwriteFillFactor = nameof(TdiffOverwriteFillFactor);
            if (env.Contains(TdiffOverwriteFillFactor))
            {
                FillFactor = double.Parse(Environment.GetEnvironmentVariable(TdiffOverwriteFillFactor));
            }
            else
            {
                FillFactor = config.DiffFillFactorToStartFullBackup;
            }
        }

        public TimeSpan ForceFullBackupTimeSpan
        {
            get => Config.EveryXTimeSpanDoFullBackup;
            set => Config.EveryXTimeSpanDoFullBackup = value;
        }

        public double FillFactor
        {
            get => Config.DiffFillFactorToStartFullBackup;
            set => Config.DiffFillFactorToStartFullBackup = value;
        }

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


            foreach (var config in Config.Cleanup.WldFiles)
            {
                if (config.AgeFilter <= TimeSpan.Zero)
                {
                    continue;
                }

                var marked = ConsumeIEnumerable(MarkOldEntries(backups.Where(backup => !backup.IsDiff),
                    config.AgeFilter,
                    config.KeepBackupEveryTimeSpan));
                Log.LogDebug($"Marked {marked} entries for {config.Name} wld cleanup");
            }

            foreach (var config in Config.Cleanup.DiffFiles)
            {
                if (config.AgeFilter <= TimeSpan.Zero)
                {
                    continue;
                }

                var marked = ConsumeIEnumerable(MarkOldEntries(backups.Where(backup => backup.IsDiff), config.AgeFilter,
                    config.KeepBackupEveryTimeSpan));
                Log.LogDebug($"Marked {marked} entries for {config.Name} diff cleanup");
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
    }
}