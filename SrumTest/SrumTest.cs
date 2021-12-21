using System;
using NUnit.Framework;
using SrumData;

namespace SrumTest;

[TestFixture]
public class SrumTest
{
    [Test]
    public void BuildingAutomation()
    {
        //var r = new Srum(@"D:\OneDrive\HPSpectreSrum\Windows\System32\SRU\SRUDB.dat",@"D:\OneDrive\HPSpectreSrum\Windows\System32\config\SOFTWARE");


        try
        {
            var r = new Srum(@"D:\OneDrive\HPSpectreSrum\2\SRU\SRUDB.dat", @"D:\OneDrive\HPSpectreSrum\2\config\SOFTWARE");
            Console.WriteLine($"r.EnergyUsages {r.EnergyUsages.Count} {EnergyUsage.TableName}");
            Console.WriteLine($"r.Unknown312 {r.Unknown312s.Count} {Unknown312.TableName}");
            Console.WriteLine($"r.UnknownD8Fs {r.UnknownD8Fs.Count} {UnknownD8F.TableName}");
            Console.WriteLine($"r.AppResourceUseInfos {r.AppResourceUseInfos.Count} {AppResourceUseInfo.TableName}");
            Console.WriteLine($"r.NetworkConnections {r.NetworkConnections.Count} {NetworkConnection.TableName}");
            Console.WriteLine($"r.NetworkUsages {r.NetworkUsages.Count} {NetworkUsage.TableName}");
            Console.WriteLine($"r.PushNotifications {r.PushNotifications.Count} {PushNotification.TableName}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // throw;
        }


        var r1 = new Srum(@"D:\OneDrive\HPSpectreSrum\2\SRUclean\SRUDB.dat", @"D:\OneDrive\HPSpectreSrum\2\config\SOFTWARE");
        //     var r = new Srum(@"C:\Temp\tout\c\Windows\System32\SRU\SRUDB.dat",null);


        Console.WriteLine($"r1.EnergyUsages {r1.EnergyUsages.Count} {EnergyUsage.TableName}");
        Console.WriteLine($"r1.Unknown312 {r1.Unknown312s.Count} {Unknown312.TableName}");
        Console.WriteLine($"r1.UnknownD8Fs {r1.UnknownD8Fs.Count} {UnknownD8F.TableName}");

        // foreach (var idMapInfo in r.PushNotifications)
        // {
        //     var user = r.UserMaps[idMapInfo.Value.UserId];
        //     var app = r.AppMaps[idMapInfo.Value.AppId];
        //
        //     
        //     
        //     Console.WriteLine($"id: {idMapInfo.Value.Id}, Time: {idMapInfo.Value.Timestamp}, User: {user.UserName}, {user.Sid}, {app.ExeInfo} , Payload Size: {idMapInfo.Value.PayloadSize}");
        //     
        // }

        // foreach (var idMapInfo in r.NetworkUsages)
        // {
        //     var user = r.UserMaps[idMapInfo.Value.UserId];
        //     var app = r.AppMaps[idMapInfo.Value.AppId];
        //
        //     
        //     
        //     Console.WriteLine($"id: {idMapInfo.Value.Id}, Time: {idMapInfo.Value.Timestamp}, User: {user.UserName}, {user.Sid}, {app.ExeInfo} , BytesReceived: {idMapInfo.Value.BytesReceived}");
        //     
        // }


        // foreach (var idMapInfo in r.EnergyUsages)
        // {
        //     var user = r.UserMaps[idMapInfo.Value.UserId];
        //     var app = r.AppMaps[idMapInfo.Value.AppId];
        //
        //     
        //     
        //     Console.WriteLine($"id: {idMapInfo.Value.Id}, Time: {idMapInfo.Value.Timestamp}, isLt: {idMapInfo.Value.IsLt} User: {user.UserName}, {user.Sid}, {app.ExeInfo} , FullChargedCapacity: {idMapInfo.Value.FullChargedCapacity} EventTS: {idMapInfo.Value.EventTimestamp}");
        //     
        // }

        // foreach (var idMapInfo in r.Unknown312s)
        // {
        //     var user = r.UserMaps[idMapInfo.Value.UserId];
        //     var app = r.AppMaps[idMapInfo.Value.AppId];
        //
        //     
        //     
        //     Console.WriteLine($"id: {idMapInfo.Value.Id}, Time: {idMapInfo.Value.Timestamp}, User: {user.UserName}, {user.Sid}, EXE: {app.ExeInfo} ,  ET: {idMapInfo.Value.EndTime} Dur: {idMapInfo.Value.DurationMs}");
        //     
        // }

        // Srum.DumpTableInfo(@"D:\OneDrive\HPSpectreSrum\Windows\System32\SRU\SRUDB.dat");
    }
}