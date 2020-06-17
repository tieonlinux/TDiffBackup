using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

#nullable enable
namespace DiffBackup.Backup
{
    public class BackupRepository
    {
        public readonly string Path;

        public BackupRepository(string path, bool allowCreation = true)
        {
            Path = System.IO.Path.GetFullPath(path);
            if (!Exists)
            {
                if (allowCreation)
                {
                    Directory.CreateDirectory(path);
                }
                else
                {
                    throw new DirectoryNotFoundException("missing directory \"{path}\"");
                }
            }
        }

        public bool Exists => Directory.Exists(Path);
        public ReadOnlyCollection<BackupRepositoryEntry> Entries => ListEntries().ToList().AsReadOnly();

        public string ToRepoPath(string realPath)
        {
            realPath = System.IO.Path.GetFullPath(realPath);
            if (!realPath.StartsWith(Path))
            {
                throw new ArgumentException($"{nameof(realPath)} is outside repository path");
            }

            var res = realPath.Substring(Path.Length);
            if (res == string.Empty)
            {
                return "./";
            }

            return res;
        }

        public string ToRealPath(string repoPath, bool check = true)
        {
            var res = System.IO.Path.Combine(Path, repoPath);
            return check ? CheckIsRepoPath(res).RealPath : res;
        }

        private static string[] SplitPath(string path)
        {
            return path.Split(new[] {System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar},
                StringSplitOptions.RemoveEmptyEntries);
        }

        public (string RelativePath, string RealPath) CheckIsRepoPath(string path)
        {
            var dir = System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(path));
            var pathDirs = SplitPath(dir);
            foreach (var (sub, i) in SplitPath(Path).Select((s, i) => (Path: s, Index: i)))
            {
                if (pathDirs.Length <= i || sub != pathDirs[i])
                {
                    throw new ArgumentException($"{nameof(path)} is not in repo");
                }
            }

            return (ToRepoPath(path), System.IO.Path.GetFullPath(path));
        }

        public BackupRepositoryEntry CreateEntryFromFile(string path, string repoPath, bool overwrite = true,
            bool createDirectories = true)
        {
            var dest = CheckIsRepoPath(System.IO.Path.Combine(Path, repoPath));
            var dir = System.IO.Path.GetDirectoryName(dest.RealPath);
            if (!Directory.Exists(dir))
            {
                if (createDirectories)
                {
                    Directory.CreateDirectory(dir);
                }
                else
                {
                    throw new DirectoryNotFoundException($"Directories \"{dest.RealPath}\" not found");
                }
            }


            File.Copy(path, dest.RealPath, overwrite);
            return new BackupRepositoryEntry(this, dest.RealPath);
        }

        private IEnumerable<BackupRepositoryEntry> ListEntries()
        {
            return ListEntries(Path);
        }

        private IEnumerable<BackupRepositoryEntry> ListEntries(string path)
        {
            foreach (var file in Directory.EnumerateFiles(System.IO.Path.GetFullPath(path)))
            {
                yield return new BackupRepositoryEntry(this, file);
            }

            foreach (var dir in Directory.EnumerateDirectories(System.IO.Path.GetFullPath(path)))
            {
                foreach (var sub in ListEntries(dir))
                {
                    yield return sub;
                }
            }
        }

        public BackupRepositoryEntry CreateEntry(string repoPath, bool existsOk = false)
        {
            var res = new BackupRepositoryEntry(this, ToRealPath(repoPath));
            using var _ = res.Open(existsOk ? FileMode.Create : FileMode.CreateNew, FileAccess.ReadWrite);
            return res;
        }

        public BackupRepositoryEntry GetEntry(string repoPath, bool checkExist = true)
        {
            var res = new BackupRepositoryEntry(this, ToRealPath(repoPath));
            if (!checkExist && !res.Exists)
            {
                throw new FileNotFoundException($"\"{res.FullName}\" not found");
            }

            return res;
        }
    }
}