using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

#nullable enable
namespace DiffBackup.Backup
{
    public static class BackupUtils
    {
        public const string DateFormat = "yyyy_MM_dd";
        public const string TimeFormat = "HH_mm_ss";
        public const string DiffFileExtension = ".diff";
        public const string WorldFileExtension = ".wld";

        private static string FormatFileName(DateTime dateTime, string referenceHash, string extension)
        {
            return $"{dateTime.ToString(TimeFormat)}_{referenceHash}{extension}";
        }

        public static string FormatDiffFileName(DateTime dateTime, string referenceHash)
        {
            return FormatFileName(dateTime, referenceHash, DiffFileExtension);
        }

        public static string FormatWorldFileName(DateTime dateTime, string referenceHash)
        {
            return FormatFileName(dateTime, referenceHash, WorldFileExtension);
        }

        private static (DateTime dateTime, string referenceHash) ParseFileName(string fileName, string extension)
        {
            var regex = new Regex(@"^(\w+)_(\w+)(\.\w+)$");
            var match = regex.Match(Path.GetFileName(fileName));
            if (match.Success && match.Groups[3].Value == extension)
            {
                return (DateTime.ParseExact(match.Groups[1].Value, TimeFormat, CultureInfo.InvariantCulture),
                    match.Groups[2].Value);
            }

            throw new FormatException($"Not a valid {extension} file name");
        }

        public static (DateTime dateTime, string referenceHash) ParseDiffFileName(string fileName)
        {
            return ParseFileName(fileName, DiffFileExtension);
        }

        public static (DateTime dateTime, string referenceHash) ParseReferenceFileName(string fileName)
        {
            return ParseFileName(fileName, WorldFileExtension);
        }

        public static string GetRepoSubFolderNameForDate(DateTime date)
        {
            return date.ToString(DateFormat);
        }

        public static string GetRepoFilePath(string worldPath)
        {
            return worldPath + ".backups";
        }

        public static string HashFile(BackupRepositoryEntry entry)
        {
            using var s = entry.Open();
            return HashFile(s);
        }

        public static string HashFile(Stream stream)
        {
            using var hasher = new SHA1Managed();
            byte[] crypto = hasher.ComputeHash(stream);
            return Base32.ToBase32String(crypto);
        }

        public static DateTime GetDateTime(BackupRepositoryEntry entry)
        {
            var fname = Path.GetFileName(entry.Name);
            var dir = Path.GetDirectoryName(entry.FullName);
            DateTime date;

            try
            {
                var time = DateTime.ParseExact(fname.Substring(0, TimeFormat.Length), TimeFormat,
                    CultureInfo.InvariantCulture);
                date = DateTime.ParseExact(dir.Trim('/', '\\'), DateFormat, CultureInfo.InvariantCulture);
                date = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentOutOfRangeException)
            {
                date = entry.LastWriteTime;
            }

            return date;
        }

        public static bool IsDiffFilePath(string path)
        {
            return Path.GetExtension(path) == DiffFileExtension;
        }

        public static bool IsWorldFilePath(string path)
        {
            return Path.GetExtension(path) == WorldFileExtension;
        }

        public static IEnumerable<BackupEntry> ListBackup(BackupRepository repo, bool returnsInvalids = false)
        {
            return ListBackup(repo.Entries, returnsInvalids);
        }

        public static IEnumerable<BackupEntry> ListBackup(IEnumerable<BackupRepositoryEntry> entries,
            bool returnsInvalids = false)
        {
            var referencePool = new Dictionary<string, BackupEntry>();
            var repositoryEntries = entries as BackupRepositoryEntry[] ?? entries.ToArray();
            foreach (var entry in repositoryEntries)
            {
                var dt = GetDateTime(entry);
                if (!IsWorldFilePath(entry.FullName))
                {
                    continue;
                }

                var refName = ParseReferenceFileName(entry.Name);
                var refEntry = new BackupEntry(entry, dt);
                referencePool[refName.referenceHash] = refEntry;
                yield return refEntry;
            }

            foreach (var entry in repositoryEntries)
            {
                var dt = GetDateTime(entry);
                var isDiff = IsDiffFilePath(entry.FullName);
                if (!isDiff)
                {
                    continue;
                }

                var (_, referenceHash) = ParseDiffFileName(entry.Name);
                if (referencePool.TryGetValue(referenceHash, out var reference))
                {
                    yield return new BackupEntry(entry, dt, true, reference);
                }
                else if (returnsInvalids)
                {
                    yield return new BackupEntry(entry, dt, true);
                }
            }
        }

        public static string[] SplitPath(string path)
        {
            return path.Split(new[] {Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar},
                StringSplitOptions.RemoveEmptyEntries);
        }

        public class BackupEntry : IEquatable<BackupEntry>
        {
            public readonly DateTime DateTime;
            public readonly BackupRepositoryEntry Entry;
            public readonly bool IsDiff;
            public readonly BackupEntry? Reference;

            public BackupEntry(BackupRepositoryEntry entry, DateTime dateTime, bool isDiff = false,
                BackupEntry? reference = null)
            {
                Entry = entry;
                DateTime = dateTime;
                IsDiff = isDiff;
                Reference = reference;
            }

            public bool Valid => !IsDiff || !ReferenceEquals(Reference, null);


            public bool Equals(BackupEntry? other)
            {
                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return Fields().Equals(other.Fields());
            }

            private Tuple<DateTime, BackupRepositoryEntry, bool, BackupEntry?> Fields()
            {
                return new Tuple<DateTime, BackupRepositoryEntry, bool, BackupEntry?>(DateTime, Entry, IsDiff,
                    Reference);
            }

            public override bool Equals(object? obj)
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

                return Equals((BackupEntry) obj);
            }

            public override int GetHashCode()
            {
                return Fields().GetHashCode();
            }
        }
    }
}