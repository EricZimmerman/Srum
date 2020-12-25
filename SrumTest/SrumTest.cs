using System;
using System.Data;
using NFluent;
using NUnit.Framework;
using SrumData;

namespace SrumTest
{
    [TestFixture]
    public class SrumTest
    {
        
        [Test]
        public void BuildingAutomation()
        {
            var r = new Srum(@"D:\OneDrive\HPSpectreSrum\Windows\System32\SRU\SRUDB.dat",@"D:\OneDrive\HPSpectreSrum\Windows\System32\config\SOFTWARE");
            
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

            foreach (var idMapInfo in r.UnknownD8Fs)
            {
                var user = r.UserMaps[idMapInfo.Value.UserId];
                var app = r.AppMaps[idMapInfo.Value.AppId];
            
                
                
                Console.WriteLine($"id: {idMapInfo.Value.Id}, Time: {idMapInfo.Value.Timestamp}, User: {user.UserName}, {user.Sid}, EXE: {app.ExeInfo} , ST {idMapInfo.Value.StartTime} ET: {idMapInfo.Value.EndTime} Dur: {idMapInfo.Value.Duration}");
                
            }

           // Srum.DumpTableInfo(@"D:\OneDrive\HPSpectreSrum\Windows\System32\SRU\SRUDB.dat");
        }
    }
}
