using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace DiffBackup.Backup
{
    public interface IBackupService : IDisposable
    {
        IBackupStrategy Strategy { get; }
        Task StartBackup(string path, DateTime? dateTime = null, CancellationToken cancellationToken = default);

        Task<IList<BackupRepositoryEntry>>
            StartCleanup(string worldPath, CancellationToken cancellationToken = default);

        List<DateTime> ListBackup(string path, CancellationToken cancellationToken);

        bool Restore(string path, DateTime date, CancellationToken cancellationToken);
    }
}