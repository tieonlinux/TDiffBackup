using System;
using System.IO;

namespace DiffBackup.Backup
{
    public class BackupRepositoryEntry
    {
        private readonly string _name;
        public readonly BackupRepository Repository;
        public string FullName => Repository.ToRepoPath(_name);
        public string Name => Path.GetFileName(_name);
        public string RealPath => Path.GetFullPath(_name);

        public long Length => FileInfo.Length;
        public bool Exists => FileInfo.Exists;

        public FileInfo FileInfo => new FileInfo(RealPath);

        public DateTime LastWriteTime
        {
            get => File.GetLastWriteTime(_name);
            set => File.SetLastWriteTime(_name, value);
        }

        internal BackupRepositoryEntry(BackupRepository repo, string realPath)
        {
            _name = Path.GetFullPath(realPath);
            Repository = repo;
        }

        public FileStream Open(FileMode mode = FileMode.Open, FileAccess? access = null,
            FileShare share = FileShare.None)
        {
            return File.Open(RealPath, mode,
                access ?? (mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite), share);
        }
    }
}