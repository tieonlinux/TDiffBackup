using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DiffBackup;
using DiffBackup.Backup;
using DiffBackup.Backup.Config;
using DiffBackup.Backup.Config.Json;
using DiffBackup.Logger;
using Equ;
using Moq;
using Topten.JsonKit_tie_tdiff;
using Xunit;
using Xunit.Abstractions;

namespace DiffBackupTests.Backup.Config.Json
{
    public class TShockConfigHandlerTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TShockConfigHandlerTests(ITestOutputHelper testOutputHelper)
        {
            JsonConverters.Install();
            _testOutputHelper = testOutputHelper;
            _temporaryPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryPath);
            _mockedLog = new Mock<ITlog>(MockBehavior.Loose);
            _mockedConfig = new Mock<AllConfig>(MockBehavior.Loose) {CallBase = true};
        }

        public void Dispose()
        {
            if (Directory.Exists(_temporaryPath))
            {
                Directory.Delete(_temporaryPath, true);
            }
        }

        private readonly string _temporaryPath;
        private readonly Mock<ITlog> _mockedLog;
        private readonly Mock<AllConfig> _mockedConfig;
        private readonly Random _random = new Random();

        [Fact]
        public void TestSaveDefaultConfig()
        {
            var configHandler = new TShockConfigHandler(_temporaryPath);
            var fileName = $"{Guid.NewGuid():N}.json";
            var path = configHandler.GetFullPath(fileName);
            Assert.False(File.Exists(path));
            configHandler.SaveConfig(new AllConfig(), fileName);
            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path, Encoding.UTF8);
            //_testOutputHelper.WriteLine(content);
            Assert.False(string.IsNullOrWhiteSpace(content));
        }

        [Fact]
        public void TestEnumSavedAsString()
        {
            var configHandler = new TShockConfigHandler(_temporaryPath);
            var fileName = $"{Guid.NewGuid():N}.json";
            var path = configHandler.GetFullPath(fileName);
            var config = new AllConfig();
            configHandler.SaveConfig(config, fileName);
            var d = Topten.JsonKit_tie_tdiff.Json.ParseFile<IDictionary<string, object>>(path);
            Assert.NotNull(d);
            Assert.NotEmpty(d);
            Assert.Equal(config.Internal.WorldSaveTrackingStrategy.ToString(),
                d.GetPath<string>("internal.worldSaveTrackingStrategy"));
        }
        
        [Fact]
        public void TestTimeSpanSavedAsString()
        {
            var configHandler = new TShockConfigHandler(_temporaryPath);
            var fileName = $"{Guid.NewGuid():N}.json";
            var path = configHandler.GetFullPath(fileName);
            var config = new AllConfig();
            configHandler.SaveConfig(config, fileName);
            var configReloaded = configHandler.LoadJson<AllConfig>(fileName);
            var d = Topten.JsonKit_tie_tdiff.Json.ParseFile<IDictionary<string, object>>(path);
            Assert.NotNull(d);
            Assert.NotEmpty(d);
            Assert.Equal("0.00:01:00:",
                d.GetPath<string>("internal.throttleTimeSpan"));
            Assert.Equal(config.Internal.ThrottleTimeSpan, configReloaded.Internal.ThrottleTimeSpan);
        }

        [Fact]
        public void TestSaveLoad2x()
        {
            var configHandler = new TShockConfigHandler(_temporaryPath);
            var fileName = $"{Guid.NewGuid():N}.json";
            var path = configHandler.GetFullPath(fileName);
            configHandler.SaveConfig(new AllConfig(), fileName);
            var firstContent = File.ReadAllText(path, Encoding.UTF8);
            var firstWriteTime = File.GetLastWriteTime(path);
            var config = configHandler.LoadJson<AllConfig>(fileName);
            configHandler.SaveConfig(config, fileName);
            Assert.Equal(firstContent, File.ReadAllText(path, Encoding.UTF8));
            Assert.True(File.GetLastWriteTime(path) > firstWriteTime);
        }
    }
}