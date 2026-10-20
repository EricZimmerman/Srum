using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace SrumTest;

/// <summary>
///     The built SrumECmd.dll, run as a separate process
/// </summary>
[TestFixture]
[NonParallelizable]
public class CliTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"srumecmd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_dir, "SRU"));
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

    //the user's temp folder: an empty one, which must stay empty (the copies go to --temp or Temp next to the program)
    private string SystemTemp => Path.Combine(_dir, "systemp");

    private static string TempNextToProgram => Path.Combine(TestContext.CurrentContext.TestDirectory, "Temp");

    private (int ExitCode, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        Directory.CreateDirectory(SystemTemp);
        foreach (var name in new[] { "TEMP", "TMP", "TMPDIR" })
        {
            start.Environment[name] = SystemTemp;
        }

        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "SrumECmd.dll"));
        foreach (var a in args)
        {
            start.ArgumentList.Add(a);
        }

        using var p = Process.Start(start)!;
        var err = p.StandardError.ReadToEndAsync();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(120_000).Should().BeTrue();
        Directory.GetFileSystemEntries(SystemTemp).Should().BeEmpty("nothing goes to the user's temp folder");
        if (Directory.Exists(TempNextToProgram))
        {
            Directory.GetDirectories(TempNextToProgram, "SrumData-*").Should().BeEmpty("the copies are deleted");
        }

        return (p.ExitCode, output + err.Result);
    }

    private static string[] Csv(string dir, string name)
    {
        var file = Directory.GetFiles(dir, $"*_SrumECmd_{name}_Output.csv").Should().ContainSingle().Subject;
        return File.ReadAllLines(file);
    }

    [Test]
    public void WritesTheCsvsWithTheirColumnsInOrder()
    {
        SrumBuilder.Standard(Path.Combine(_dir, "SRU", "SRUDB.dat"));
        var csv = Path.Combine(_dir, "out");

        var (code, output) = Run("-d", _dir, "--csv", csv);

        code.Should().Be(0, output);

        //the existing columns first, in their order (Timeline Explorer matches these headers); new ones at the end
        Csv(csv, "NetworkUsages")[0].Should().Be("Id,Timestamp,ExeInfo,ExeInfoDescription,ExeTimestamp,SidType,Sid,UserName,UserId,AppId,BytesReceived,BytesSent,InterfaceLuid,InterfaceType,L2ProfileFlags,L2ProfileId,ProfileName");
        Csv(csv, "AppTimelineProvider")[0].Should().Be("Id,Timestamp,ExeInfo,ExeInfoDescription,ExeTimestamp,SidType,Sid,UserName,UserId,AppId,EndTime,DurationMs,InFocusS,PsmForegroundS,UserInputS,KeyboardInputS,MouseInputS,DisplayRequiredS,AudioInS,AudioOutS");
        Csv(csv, "TotalUsed")[0].Should().Be("Id,Timestamp,ExeInfo,ExeInfoDescription,ExeTimestamp,SidType,Sid,UserName,UserId,AppId,Total,Used");
        Directory.GetFiles(csv, "*ProcessorTime*").Should().BeEmpty("the new tables' CSVs are only written when they have rows");

        Csv(csv, "NetworkUsages").Skip(1).Should().HaveCount(3);
        Csv(csv, "NetworkUsages")[1].Should().StartWith(@"1,2024-05-06 07:08:00,\Device\HarddiskVolume3\Windows\System32\svchost.exe,");
    }

    [Test]
    public void DtAppliesToTimestamp()
    {
        SrumBuilder.Standard(Path.Combine(_dir, "SRU", "SRUDB.dat"));
        var csv = Path.Combine(_dir, "out");

        Run("-f", Path.Combine(_dir, "SRU", "SRUDB.dat"), "--csv", csv, "--dt", "yyyy-MM-ddTHH:mm:ss").ExitCode.Should().Be(0);

        Csv(csv, "NetworkUsages")[1].Should().StartWith(@"1,2024-05-06T07:08:00,");
    }

    [Test]
    public void ExitCodes()
    {
        Run().ExitCode.Should().Be(1, "-f or -d is required");
        Run("-f", Path.Combine(_dir, "missing.dat"), "--csv", Path.Combine(_dir, "out")).ExitCode.Should().Be(1);
        Run("-d", _dir, "--csv", Path.Combine(_dir, "out")).ExitCode.Should().Be(1, "there is no SRUDB.dat");

        File.WriteAllBytes(Path.Combine(_dir, "SRU", "SRUDB.dat"), new byte[8192]);
        Run("-d", _dir).ExitCode.Should().Be(1, "--csv is required");
        Run("-d", _dir, "--csv", Path.Combine(_dir, "out")).ExitCode.Should().Be(1, "the database can't be opened");
    }
    [Test]
    public void CopiesGoToTempNextToTheProgram()
    {
        SrumBuilder.Standard(Path.Combine(_dir, "SRU", "SRUDB.dat"));
        var existed = Directory.Exists(TempNextToProgram);

        var (code, output) = Run("-d", _dir, "--csv", Path.Combine(_dir, "out"));

        code.Should().Be(0, output);
        //the copies' folder is deleted, and Temp too when this run made it and it is empty
        if (Directory.Exists(TempNextToProgram))
        {
            existed.Should().BeTrue("Temp is removed when the run created it and it is empty");
        }
    }

    [Test]
    public void CopiesGoToTheTempGiven()
    {
        SrumBuilder.Standard(Path.Combine(_dir, "SRU", "SRUDB.dat"));
        var temp = Directory.CreateDirectory(Path.Combine(_dir, "my temp")).FullName;

        var (code, output) = Run("-d", _dir, "--csv", Path.Combine(_dir, "out"), "--temp", temp);

        code.Should().Be(0, output);
        Directory.Exists(temp).Should().BeTrue("a folder that was there is kept");
        Directory.GetFileSystemEntries(temp).Should().BeEmpty("the copies are deleted when done");

        //one that wasn't there is created, and removed again
        var newTemp = Path.Combine(_dir, "new temp");
        Run("-d", _dir, "--csv", Path.Combine(_dir, "out2"), "--temp", newTemp).ExitCode.Should().Be(0);
        Directory.Exists(newTemp).Should().BeFalse();
    }

    [Test]
    public void TempThatCantBeWrittenStops()
    {
        SrumBuilder.Standard(Path.Combine(_dir, "SRU", "SRUDB.dat"));
        //a folder under a file can't be created
        var file = Path.Combine(_dir, "a file");
        File.WriteAllText(file, "x");

        var (code, output) = Run("-d", _dir, "--csv", Path.Combine(_dir, "out"), "--temp", Path.Combine(file, "temp"));

        code.Should().Be(1, output);
        output.Should().Contain("Give one with --temp");
        Directory.Exists(Path.Combine(_dir, "out")).Should().BeFalse("nothing was processed");
    }
}
