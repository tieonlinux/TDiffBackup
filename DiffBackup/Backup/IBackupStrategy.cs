using System;

#nullable enable
namespace DiffBackup.Backup
{
    public interface IBackupStrategy
    {
        BackupRepositoryEntry? GetReference(BackupRepository repo, DateTime date);
    }
}