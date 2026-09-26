using System;
using System.Collections.Generic;
using System.IO;
using EveOPreview.Services.Implementation;
using Xunit;

namespace EveOPreview.Tests.Checks;

public sealed class EliteCommanderResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "EliteO-Tests-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);

    public EliteCommanderResolverTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    [Theory]
    [InlineData("{ \"timestamp\":\"2026-09-25T20:22:10Z\", \"event\":\"Commander\", \"FID\":\"F1\", \"Name\":\"Pin-Lui\" }", "Pin-Lui")]
    [InlineData("{ \"timestamp\":\"2026-09-25T20:22:30Z\", \"event\":\"LoadGame\", \"FID\":\"F1\", \"Commander\":\"DIRTYRODRIGUEZ\", \"Horizons\":true }", "DIRTYRODRIGUEZ")]
    [InlineData("{ \"timestamp\":\"2026-09-25T20:21:49Z\", \"event\":\"Fileheader\", \"part\":1 }", null)]
    [InlineData("{ \"event\":\"ReceiveText\", \"Message\":\"Commander hello\" }", null)]
    [InlineData("{ \"event\":\"Commander\", \"Name\":\"\" }", null)]
    [InlineData("{ \"event\":\"Commander\", \"Name\":\"Half", null)]
    [InlineData("", null)]
    public void ExtractsCommanderFromJournalLines(string line, string expected)
    {
        Assert.Equal(expected, EliteCommanderResolver.ExtractCommander(line));
    }

    [Theory]
    [InlineData(@"C:\j\Journal.2026-09-25T202149.01.log", @"C:\j\Journal.2026-09-25T202149.02.log", true)]
    [InlineData(@"C:\j\Journal.2026-09-25T202149.01.log", @"C:\j\Journal.2026-09-26T000513.01.log", false)]
    [InlineData(null, @"C:\j\Journal.2026-09-26T000513.01.log", false)]
    public void RecognisesContinuationFiles(string previous, string next, bool same)
    {
        Assert.Equal(same, EliteCommanderResolver.IsSameSession(previous, next));
    }

    [Fact]
    public void ShowsUserBeforeLoginAndCommanderAfter()
    {
        var folder = UserFolder("S-1", out var resolver, fileOwners: null);
        var journal = Path.Combine(folder, "Journal.2026-09-26T000513.01.log");
        File.WriteAllText(journal, "{ \"event\":\"Fileheader\", \"part\":1 }\n");
        Touch(journal, _now);

        Assert.Equal("Elite - CMDR_Rodrigez (not logged in)", resolver.GetClientTitle(7328, _now.AddSeconds(-5)));

        File.AppendAllText(journal, "{ \"event\":\"Commander\", \"FID\":\"F2\", \"Name\":\"DIRTYRODRIGUEZ\" }\n{ \"event\":\"Mus");
        Touch(journal, _now);
        _now = _now.AddSeconds(3);
        Assert.Equal("Elite - DIRTYRODRIGUEZ", resolver.GetClientTitle(7328, _now.AddSeconds(-8)));
    }

    [Fact]
    public void IgnoresJournalsFromBeforeTheGameStarted()
    {
        var folder = UserFolder("S-1", out var resolver, fileOwners: null);
        var old = Path.Combine(folder, "Journal.2026-09-24T191642.01.log");
        File.WriteAllText(old, "{ \"event\":\"Commander\", \"Name\":\"OLD-CMDR\" }\n");
        Touch(old, _now.AddHours(-5));

        Assert.Equal("Elite - CMDR_Rodrigez (not logged in)", resolver.GetClientTitle(7328, _now.AddMinutes(-2)));
    }

    [Fact]
    public void UsesOpenFileOwnerWhenTwoGamesShareOneFolder()
    {
        var owners = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        var folder = UserFolder("S-SAME", out var resolver, owners);
        var a = Path.Combine(folder, "Journal.2026-09-26T000100.01.log");
        var b = Path.Combine(folder, "Journal.2026-09-26T000200.01.log");
        File.WriteAllText(a, "{ \"event\":\"Commander\", \"Name\":\"ALPHA\" }\n");
        File.WriteAllText(b, "{ \"event\":\"Commander\", \"Name\":\"BRAVO\" }\n");
        Touch(a, _now);
        Touch(b, _now.AddSeconds(1));
        owners[a] = new[] { 100 };
        owners[b] = new[] { 200 };

        resolver.GetClientTitle(100, _now.AddMinutes(-1));
        resolver.GetClientTitle(200, _now.AddMinutes(-1));
        _now = _now.AddSeconds(3);

        Assert.Equal("Elite - ALPHA", resolver.GetClientTitle(100, _now.AddMinutes(-1)));
        Assert.Equal("Elite - BRAVO", resolver.GetClientTitle(200, _now.AddMinutes(-1)));
    }

    [Fact]
    public void UnknownOwnerStillGivesATitle()
    {
        var resolver = new EliteCommanderResolver(_ => (null, null), _ => null, _ => Array.Empty<int>(), () => _now);
        Assert.Equal("Elite - Unknown (not logged in)", resolver.GetClientTitle(1, _now));
    }

    private string UserFolder(string sid, out EliteCommanderResolver resolver, Dictionary<string, int[]> fileOwners)
    {
        var folder = Path.Combine(_root, sid, "Saved Games", "Frontier Developments", "Elite Dangerous");
        Directory.CreateDirectory(folder);
        resolver = new EliteCommanderResolver(
            _ => (sid, "CMDR_Rodrigez"),
            s => s == sid ? folder : null,
            path => fileOwners != null && fileOwners.TryGetValue(path, out var pids) ? pids : Array.Empty<int>(),
            () => _now);
        return folder;
    }

    private static void Touch(string path, DateTime utc) => File.SetLastWriteTimeUtc(path, utc);
}
