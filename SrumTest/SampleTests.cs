using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using SrumData;

namespace SrumTest;

/// <summary>
///     The DFIR Artifact Museum's Windows/SRUM samples (MIT): SRUM_SAMPLES, or Corpus/SRUM in a parent folder of the
///     tests. The counts are the tables' row counts (also what SrumECmd 2026.5.0 exported from copies recovered with
///     esentutl /r sru /d /i). Skipped without the samples (they are not in the repository)
/// </summary>
[TestFixture]
[NonParallelizable]
public class SampleTests
{
    private static string Root()
    {
        var root = Environment.GetEnvironmentVariable("SRUM_SAMPLES");

        if (string.IsNullOrEmpty(root))
        {
            for (var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); d != null; d = d.Parent)
            {
                var candidate = Path.Combine(d.FullName, "Corpus", "SRUM");
                if (Directory.Exists(candidate))
                {
                    root = candidate;
                    break;
                }
            }
        }

        if (root == null || !Directory.Exists(root))
        {
            Assert.Ignore("SRUM samples not found (SRUM_SAMPLES or Corpus/SRUM)");
        }

        return root;
    }

    //energy, timeline, vfuprov, app resource, network connections, network usage, push notifications
    [TestCase(@"Win10\APTSimulatorVM\Clean", 0, 1660, 26, 562, 6, 96, 5)]
    [TestCase(@"Win10\BelkasoftCTF_InsiderThreat\Clean", 1, 4107, 101, 2087, 50, 465, 0)]
    [TestCase(@"Win10\BelkasoftCTF_InsiderThreat\Dirty", 1, 4107, 101, 2087, 50, 465, 0)]
    [TestCase(@"Win10\RathbunVM\Clean", 0, 163, 4, 118, 1, 23, 0)]
    [TestCase(@"Win10\RathbunVM\Dirty", 0, 163, 4, 118, 1, 23, 0)]
    [TestCase(@"Win11\RathbunVM\Clean", 15, 791, 33, 662, 9, 143, 0)]
    [TestCase(@"Win11\RathbunVM\Dirty", 15, 791, 33, 662, 9, 143, 0)]
    [TestCase(@"Server2022\APTSimulatorVM\APTSimulatorVM_SRUM_Clean_Server2022\SRU", 0, 0, 0, 0, 0, 0, 0)]
    [TestCase(@"Server2022\APTSimulatorVM\APTSimulatorVM_SRUM_Clean_Server2022_1DayLater\SRU", 0, 0, 0, 153, 4, 0, 0)]
    public void Counts(string sample, int energy, int timeline, int vfuprov, int appResource, int connections, int usage, int push)
    {
        var dir = Path.Combine(Root(), sample);
        var before = SrumTests.Snapshot(dir);

        var srum = new Srum(Path.Combine(dir, "SRUDB.dat"), null);

        srum.ParseErrors.Should().BeEmpty();
        srum.Warnings.Should().NotContain(w => w.Contains("esentutl"), "the logs are there: they are replayed, no repair");

        srum.EnergyUsages.Should().HaveCount(energy);
        srum.TimelineProviders.Should().HaveCount(timeline);
        srum.Vfuprovs.Should().HaveCount(vfuprov);
        srum.AppResourceUseInfos.Should().HaveCount(appResource);
        srum.NetworkConnections.Should().HaveCount(connections);
        srum.NetworkUsages.Should().HaveCount(usage);
        srum.PushNotifications.Should().HaveCount(push);

        SrumTests.Snapshot(dir).Should().BeEquivalentTo(before, "the samples are read from copies");
    }

    [Test]
    public void Server2022Providers()
    {
        var srum = new Srum(Path.Combine(Root(), @"Server2022\APTSimulatorVM\APTSimulatorVM_SRUM_Clean_Server2022_1DayLater\SRU\SRUDB.dat"), null);

        srum.Warnings.Should().BeEmpty("their object ids are in the id map (IdType 4)");

        srum.ProcessorTimes.Values.Select(p => (p.ExeInfo, p.ProcessorTime)).Should().Equal(("Total", 9L), ("Total", 1L));
        srum.SizeInBytesInfos.Values.Should().OnlyContain(s => s.SizeInBytes == 64424509440L);
        srum.TotalUsedInfos.Should().HaveCount(4);
        srum.TotalUsedInfos[1].ExeInfo.Should().StartWith("7b62f03c-0496-4abe-a51c-bfadd0086eb6\tC");
        srum.TotalUsedInfos[1].Total.Should().Be(64078475264L);
        srum.TotalUsedInfos[1].Used.Should().Be(12385419264L);
        srum.NetworkBytesInfos.Values.Should().OnlyContain(n => n.ExeInfo.Contains("Intel(R) 82574L Gigabit Network Connection"));
    }
}
