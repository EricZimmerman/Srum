using System;
using NFluent;
using NUnit.Framework;
using SrumData;

namespace SrumTest
{
    [TestFixture]
    public class SrumTest
    {
        
        [Test]
        public void BcdHiveShouldHaveBcdHiveType()
        {
            var r = new Srum(@"C:\Temp\SRUDB.dat",@"C:\Temp\toutReg\C\Windows\System32\config\SOFTWARE");

            
            foreach (var idMapInfo in r.NetworkUsages)
            {
                var user = idMapInfo.Value.UserIdMapInfo.RawValue;

                if (r.SidToUser.ContainsKey(user))
                {
                    user = r.SidToUser[user];
                }
                
                Console.WriteLine($"id: {idMapInfo.Value.Id}, Time: {idMapInfo.Value.Timestamp}, User: {user}, {idMapInfo.Value.AppIdMapInfo.ExeInfo}, Sent: {idMapInfo.Value.BytesSent}, Received: {idMapInfo.Value.BytesReceived}");
                
            }
        }
    }
}
