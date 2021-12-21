using Archipelago.MultiClient.Net.Packets;
using ArchipelagoProxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CommandLineTester
{
    public class Program
    {
        private static List<int> locChecks = new List<int>();
        public static void Main(string[] args)
        {
            var tst = new ArchipelagoProxy.ArchipelagoProxy("localhost");
            tst.RaftItemUnlockedForCurrentWorld += Tst_RaftItemUnlockedForCurrentWorld;
            tst.PrintMessage += Tst_PrintMessage;
            tst.Connect("SunnyBat-Raft", "");
            while (true)
            {
                var nextLine = Console.ReadLine();
                if (nextLine.StartsWith("UL"))
                {
                    var idStr = nextLine.Substring(2).Trim();
                    try
                    {
                        var locationId = int.Parse(idStr);
                        locChecks.Add(locationId);
                        tst.LocationFromCurrentWorldUnlocked(locationId);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Invalid input");
                    }
                }
                else if (nextLine == "S")
                {
                    tst._session.Socket.SendPacket(new SyncPacket());
                }
                else if (nextLine == "CR")
                {
                    tst._session.Socket.SendPacket(new StatusUpdatePacket() { Status = Archipelago.MultiClient.Net.Enums.ArchipelagoClientState.ClientReady });
                }
                else if (nextLine == "CP")
                {
                    tst._session.Socket.SendPacket(new StatusUpdatePacket() { Status = Archipelago.MultiClient.Net.Enums.ArchipelagoClientState.ClientPlaying });
                }
                else if (nextLine == "GDP")
                {
                    tst._session.Socket.SendPacket(new GetDataPackagePacket());
                }
                else if (nextLine == "LC")
                {
                    tst._session.Socket.SendPacket(new LocationChecksPacket() { Locations = locChecks });
                }
                else if (nextLine.StartsWith("GLFN"))
                {
                    var locationName = nextLine.Substring(4).Trim();
                    Console.WriteLine(locationName + " = " + tst._session.Locations.GetLocationIdFromName("Raft", locationName));
                }
                else
                {
                    Console.WriteLine(nextLine);
                }
            }
        }

        private static void Tst_PrintMessage(string obj)
        {
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine("PM: " + obj);
        }

        private static void Tst_RaftItemUnlockedForCurrentWorld(int arg1, string arg2)
        {
            Console.WriteLine("RIUFCW: " + arg1 + ", " + arg2);
        }
    }
}
