using System;
using System.Collections.Generic;

#nullable enable
namespace DiffBackup.Backup
{
    public interface IBackupStrategy : IDisposable
    {
        BackupRepositoryEntry? GetReference(BackupRepository repo, DateTime date);

        IList<BackupRepositoryEntry> ListExpiredEntries(BackupRepository repo, DateTime date);
    }
}