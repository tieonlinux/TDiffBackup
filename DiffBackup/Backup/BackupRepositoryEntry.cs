using System;
using System.IO;

#nullable enable
namespace DiffBackup.Backup
{
    public class BackupRepositoryEntry : IEquatable<BackupRepositoryEntry>, IComparable<BackupRepositoryEntry>,
        IComparable
    {
        private readonly string _name;
        public readonly BackupRepository Repository;

        internal BackupRepositoryEntry(BackupRepository repo, string realPath)
        {
            _name = Path.GetFullPath(realPath);
            Repository = repo;
        }

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

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return 1;
            }

            if (ReferenceEquals(this, obj))
            {
                return 0;
            }

            return obj is BackupRepositoryEntry other
                ? CompareTo(other)
                : throw new ArgumentException($"Object must be of type {nameof(BackupRepositoryEntry)}");
        }

        public int CompareTo(BackupRepositoryEntry? other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            return string.Compare(_name, other._name, StringComparison.InvariantCulture);
        }

        public bool Equals(BackupRepositoryEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return FullName == other.FullName && RealPath == other.RealPath;
        }

        public FileStream Open(FileMode mode = FileMode.Open, FileAccess? access = null,
            FileShare share = FileShare.None)
        {
            return File.Open(RealPath, mode,
                access ?? (mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite), share);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((BackupRepositoryEntry) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((RealPath != null ? RealPath.GetHashCode() : 0) * 397) ^
                       (FullName != null ? FullName.GetHashCode() : 0);
            }
        }
    }
}