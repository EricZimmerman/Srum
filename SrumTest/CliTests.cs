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

    private static (int ExitCode, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "SrumECmd.dll"));
        foreach (var a in args)
        {
            start.ArgumentList.Add(a);
        }

        using var p = Process.Start(start)!;
        var err = p.StandardError.ReadToEndAsync();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(120_000).Should().BeTrue();
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
}
