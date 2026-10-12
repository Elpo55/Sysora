using System.Globalization;
using Microsoft.Extensions.Logging;
using Sysora.Infrastructure;
using Sysora.Infrastructure.Logging;

namespace Sysora.Tests.Services;

public sealed class FileLoggerProviderTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("sysora-log-tests-");

    private SysoraPaths Paths => new(_root.FullName);

    private string TodayFile(string prefix) =>
        Path.Combine(Paths.LogsDirectory, prefix + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void TwoWritersOfTheSameFileKeepEveryLine()
    {
        // Two instances writing the same daily file (one closing while the next starts): each line goes after the
        // other's instead of overwriting it.
        var first = new FileLoggerProvider(Paths, new LogLevelSwitch());
        var second = new FileLoggerProvider(Paths, new LogLevelSwitch());
        var file = TodayFile(FileLoggerProvider.DefaultFilePrefix);

        first.CreateLogger("First").LogInformation("one");
        WaitFor(file, "First: one");
        second.CreateLogger("Second").LogInformation("two, a longer line than the next one");
        WaitFor(file, "Second: two, a longer line than the next one");
        first.CreateLogger("First").LogInformation("three");
        first.Dispose();
        second.Dispose();

        var lines = ReadShared(file).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.EndsWith("First: one", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("Second: two, a longer line than the next one", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("First: three", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void DemoModeWritesItsOwnFile()
    {
        var demo = new FileLoggerProvider(Paths, new LogLevelSwitch(), FileLoggerProvider.DemoFilePrefix);
        demo.CreateLogger("Demo").LogInformation("simulated");
        demo.Dispose();

        Assert.Contains("Demo: simulated", ReadShared(TodayFile(FileLoggerProvider.DemoFilePrefix)), StringComparison.Ordinal);
        Assert.False(File.Exists(TodayFile(FileLoggerProvider.DefaultFilePrefix)));
    }

    private static void WaitFor(string file, string text)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!File.Exists(file) || !ReadShared(file).Contains(text, StringComparison.Ordinal))
        {
            Assert.True(DateTime.UtcNow < deadline, $"\"{text}\" was not written.");
            Thread.Sleep(20);
        }
    }

    private static string ReadShared(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
