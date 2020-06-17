using System;
using System.Linq;

#nullable enable
namespace DiffBackup.Backup
{
    public class DefaultBackupStrategy : IBackupStrategy
    {
        public DefaultBackupStrategy()
        {
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
    }
}