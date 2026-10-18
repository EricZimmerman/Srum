using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;
using SrumData;

namespace SrumTest;

[TestFixture]
[NonParallelizable]
public class SrumTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"srumtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private string Db => Path.Combine(_dir, "SRUDB.dat");

    internal static System.Collections.Generic.Dictionary<string, string> Snapshot(string dir)
    {
        using var sha = SHA1.Create();
        return Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .ToDictionary(f => f, f => Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(f))));
    }

    [Test]
    public void BadValuesKeepTheirRowsAndTables()
    {
        SrumBuilder.Standard(Db);

        var srum = new Srum(Db, null);

        //a bad "!!" time stamp and a SID that is not whole sub authorities lost the id map (and so everything)
        srum.AppMaps.Should().HaveCount(4);
        srum.AppMaps[11].ExeInfo.Should().Be("app.exe");
        srum.AppMaps[11].ExeInfoDescription.Should().Be("App description");
        srum.AppMaps[11].Timestamp.Should().BeNull();
        srum.UserMaps[20].Sid.Should().Be("S-1-5-21-1-2-3-1001");
        srum.UserMaps[21].Sid.Should().Be("S-1-5");

        //ids not in the map lost the whole table
        srum.NetworkUsages.Should().HaveCount(3);
        srum.NetworkUsages[1].ExeInfo.Should().Be(@"\Device\HarddiskVolume3\Windows\System32\svchost.exe");
        srum.NetworkUsages[1].Sid.Should().Be("S-1-5-21-1-2-3-1001");
        srum.NetworkUsages[1].InterfaceType.Should().Be(InterfaceType.IF_TYPE_IEEE80211);
        srum.NetworkUsages[3].ExeInfo.Should().BeNull();
        srum.Warnings.Should().ContainSingle().Which.Should().Contain("2 app or user id(s) are not in SruDbIdMapTable");

        //a FILETIME out of range lost the whole table
        srum.TimelineProviders.Should().HaveCount(2);
        srum.TimelineProviders[1].EndTime.Should().Be(new DateTimeOffset(2024, 5, 6, 12, 0, 0, TimeSpan.Zero));
        srum.TimelineProviders[1].InFocusS.Should().Be(30);
        srum.TimelineProviders[1].UserInputS.Should().Be(3);
        srum.TimelineProviders[1].AudioOutS.Should().Be(4);
        srum.TimelineProviders[1].KeyboardInputS.Should().BeNull("the table has no such column");
        srum.TimelineProviders[2].EndTime.Should().Be(DateTimeOffset.MinValue);
        srum.ParseErrors.Should().ContainSingle().Which.Should().Contain("EndTime");
    }

    [Test]
    public void ObjectsOfTheNewerProvidersAreNamed()
    {
        //IdType 4 was skipped
        SrumBuilder.Standard(Db);

        var srum = new Srum(Db, null);

        srum.AppMaps[30].MapType.Should().Be(MapType.Object);
        var tu = srum.TotalUsedInfos.Should().ContainSingle().Subject.Value;
        tu.ExeInfo.Should().Be("Total");
        tu.Total.Should().Be(64078475264L);
        tu.Used.Should().Be(12385419264L);
        tu.Timestamp.Should().Be(new DateTimeOffset(2023, 10, 18, 3, 30, 0, TimeSpan.Zero));
    }

    [Test]
    public void TheFolderIsNotChanged()
    {
        SrumBuilder.Standard(Db, dirty: true);
        foreach (var f in Directory.GetFiles(_dir))
        {
            File.SetAttributes(f, FileAttributes.ReadOnly);
        }

        var before = Snapshot(_dir);

        var srum = new Srum(Db, null);

        srum.NetworkUsages.Should().HaveCount(3);
        Snapshot(_dir).Should().BeEquivalentTo(before);

        foreach (var f in Directory.GetFiles(_dir))
        {
            File.SetAttributes(f, FileAttributes.Normal);
        }
    }

    [Test]
    public void ADirtyDatabaseIsBroughtUpToDateFromItsLogs()
    {
        SrumBuilder.Standard(Db, dirty: true);
        Directory.GetFiles(_dir, "SRU*.log").Should().NotBeEmpty();

        var srum = new Srum(Db, null);

        srum.Warnings.Should().NotContain(w => w.Contains("esentutl"), "the logs were replayed: no repair");
        srum.NetworkUsages.Should().HaveCount(3);
        srum.TimelineProviders.Should().HaveCount(2);
    }

    [Test]
    public void ADirtyDatabaseWithoutItsLogsIsRepairedFromACopy()
    {
        SrumBuilder.Standard(Db, dirty: true);
        foreach (var f in Directory.GetFiles(_dir).Where(f => !f.EndsWith("SRUDB.dat", StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(f);
        }

        var srum = new Srum(Db, null);

        //what was flushed to the database before the dirty shutdown varies by machine (none here, some on CI runners), so
        //the repaired copy may have some rows: only the synthetic bad EndTime may be reported
        srum.Warnings.Should().Contain(w => w.Contains("esentutl /p"));
        srum.ParseErrors.Should().OnlyContain(e => e.Contains("EndTime"));
    }

    [Test]
    public void ADatabaseThatCantBeOpenedThrows()
    {
        File.WriteAllBytes(Db, Enumerable.Repeat((byte) 0x41, 64 * 1024).ToArray());

        var act = () => new Srum(Db, null);

        act.Should().Throw<Exception>();
        Directory.GetDirectories(Path.GetTempPath(), "SrumData-*").Should().NotContain(d => Directory.GetFiles(d, "SRUDB.dat").Any(),
            "the temporary folder is deleted");
        new Action(() => new Srum(Path.Combine(_dir, "missing.dat"), null)).Should().Throw<FileNotFoundException>();
    }

    [Test]
    public void ADatabaseWithoutTheUsualTablesIsRead()
    {
        //tables vary by Windows version; a missing one is not an error
        SrumBuilder.Create(Db, (s, db) => SrumBuilder.IdMapTable(s, db, new (int, byte, byte[])[] {(1, 0, SrumBuilder.Text("a.exe"))}));

        var srum = new Srum(Db, null);

        srum.ParseErrors.Should().BeEmpty();
        srum.Warnings.Should().BeEmpty();
        srum.AppMaps.Should().ContainSingle();
        srum.NetworkUsages.Should().BeEmpty();
    }

    [TestCase(new byte[] {1, 1, 0, 0, 0, 0, 0, 5, 18, 0, 0, 0}, "S-1-5-18")]
    [TestCase(new byte[] {1, 2, 0, 0, 0, 0, 0, 5, 32, 0, 0, 0, 0x20, 2, 0, 0}, "S-1-5-32-544")]
    [TestCase(new byte[] {1, 5, 0, 0, 0, 0, 0, 5, 0x15, 0, 0, 0, 0xA0, 0x65, 0xCF, 0x7E, 0x78, 0x4B, 0x9B, 0x5F, 0xE7, 0x7C, 0x87, 0x70, 0x09, 0x1C, 0x01, 0x00}, "S-1-5-21-2127521184-1604012920-1887927527-72713")]
    [TestCase(new byte[] {1, 1, 0, 0, 0, 0, 0, 5, 18}, "S-1-5")]
    [TestCase(new byte[] {1, 1, 0}, "")]
    public void Sids(byte[] blob, string expected)
    {
        Srum.ConvertHexStringToSidString(blob).Should().Be(expected);
    }

    [Test]
    public void AppInfoWithTheTimeStampFormat()
    {
        var app = new AppInfo(1, MapType.NormalApp, "!!wscript.exe!2019/12/07:09:10:02!1bd3f!Microsoft ® Windows Based Script Host");

        app.ExeInfo.Should().Be("wscript.exe");
        app.Timestamp.Should().Be(new DateTimeOffset(2019, 12, 7, 9, 10, 2, TimeSpan.Zero));
        app.Unknown.Should().Be("1bd3f");
        app.ExeInfoDescription.Should().Be("Microsoft ® Windows Based Script Host");

        new AppInfo(2, MapType.NormalApp, null).ExeInfo.Should().BeEmpty();
    }
}
