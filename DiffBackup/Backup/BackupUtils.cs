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
        
        public static string FormatDiffFileName(DateTime dateTime, string referenceHash)
        {
            return $"{dateTime.ToString(TimeFormat)}_{referenceHash}{DiffFileExtension}";
        }

        public static (DateTime dateTime, string referenceHash) ParseDiffFileName(string fileName)
        {
            var regex = new Regex(@"^(\w+)_(\w+)(\.\w+)$");
            var match = regex.Match(Path.GetFileName(fileName));
            if (match.Success && match.Groups[3].Value == DiffFileExtension)
            {
                return (DateTime.ParseExact(match.Groups[1].Value, TimeFormat, CultureInfo.InvariantCulture), match.Groups[2].Value);
            }
            throw new FormatException("Not a valid diff file name");
        }
        
        public static string ParseReferenceFileName(string fileName)
        {
            var regex = new Regex(@"^(\w+)(\.\w+)$");
            var match = regex.Match(Path.GetFileName(fileName));
            if (match.Success && match.Groups[2].Value == WorldFileExtension)
            {
                return match.Groups[1].Value;
            }
            throw new FormatException("Not a valid reference file name");
        }
        
        public static string FormatWorldFileName(DateTime dateTime, string reference)
        {
            return $"{reference}{WorldFileExtension}";
        }

        public static string GetRepoSubFolderNameForDate(DateTime date)
        {
            return date.ToString(DateFormat);
        }

        public static string GetRepoFilePath(string worldPath) => worldPath + ".backups";

        public static string HashFile(BackupRepositoryEntry entry)
        {
            using var s = entry.Open();
            return HashFile(s);
        }
        
        public static string HashFile(Stream stream)
        {
            using var hasher = new SHA256Managed();
            string hash = string.Empty;
            byte[] crypto = hasher.ComputeHash(stream);
            hash = crypto.Aggregate(hash, (current, theByte) => $"{current}{theByte:x2}");
            return hash;
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

        public class BackupEntry
        {
            public readonly BackupRepositoryEntry Entry;
            public readonly DateTime DateTime;
            public readonly bool IsDiff;
            public readonly BackupEntry? Reference;

            public BackupEntry(BackupRepositoryEntry entry, DateTime dateTime, bool isDiff = false, BackupEntry? reference = null)
            {
                Entry = entry;
                DateTime = dateTime;
                IsDiff = isDiff;
                Reference = reference;
            }
        }

        public static IEnumerable<BackupEntry> ListBackup(BackupRepository repo)
        {
            return ListBackup(repo.Entries);
        }

        public static IEnumerable<BackupEntry> ListBackup(IEnumerable<BackupRepositoryEntry> entries)
        {
            
            var referencePool = new Dictionary<string, BackupEntry>();
            var repositoryEntries = entries as BackupRepositoryEntry[] ?? entries.ToArray();
            foreach (var entry in repositoryEntries)
            {
                var dt = GetDateTime(entry);
                if (!IsWorldFilePath(entry.FullName)) continue;
                var refName = ParseReferenceFileName(entry.Name);
                var refEntry = new BackupEntry(entry, dt);
                referencePool[refName] = refEntry;
                yield return refEntry;
            }

            foreach (var entry in repositoryEntries)
            {
                var dt = GetDateTime(entry);
                var isDiff = IsDiffFilePath(entry.FullName);
                if (!isDiff) continue;
                var (_, referenceHash) = ParseDiffFileName(entry.Name);
                if (referencePool.TryGetValue(referenceHash, out var reference))
                {
                    yield return new BackupEntry(entry, dt, true, reference);
                }
            }
        }
    }
}