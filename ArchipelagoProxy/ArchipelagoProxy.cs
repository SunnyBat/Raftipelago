using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArchipelagoProxy
{
    public class ArchipelagoProxy
    {
        /*
         * Open Questions
         * - Is the StatusUpdate packet's ClientState.ClientGoal (which is documented as "Goal Completion") expected to be set by us automatically, or is this a "Complete"/"Forfeit" command thing?
         * -- Maybe depends on configuration of run?
         * -- Does MultiClient.Net do this for us?
         */

        // Events TO Raft
        /// <summary>
        /// Called when a new Raft item is unlocked for the current world.
        /// </summary>
        public event Action<int, string> RaftItemUnlockedForCurrentWorld;
        /// <summary>
        /// Called when a message is received. This can be a chat message, an item received message, etc
        /// </summary>
        public event Action<string> PrintMessage;

        public readonly ArchipelagoSession _session; // TODO Make private

        // Tracking between Archipelago Server <-> Proxy <-> Raft
        private bool isRaftWorldLoaded = false;
        private int hintCost = 0;
        private int currentHintPoints = 0;
        public ArchipelagoProxy(string urlToHost)
        {
            _session = ArchipelagoSessionFactory.CreateSession(urlToHost);
            _session.Items.ItemReceived += itemHelper => // Called once per new item, don't loop dequeue (though it only enqueus one item at a time anyways)
            {
                try
                {
                    PrintMessage("IR1");
                    var nextItem = itemHelper.DequeueItem();
                    PrintMessage("IR2");
                    RaftItemUnlockedForCurrentWorld(nextItem.Item, _session.Players.GetPlayerName(nextItem.Player));
                    PrintMessage("IR3");
                }
                catch (Exception e)
                {
                    if (PrintMessage != null)
                    {
                        PrintMessage(e.Message);
                    }
                }
            };
            _session.Socket.PacketReceived += packet =>
            {
                HandlePacket(packet);
            };
        }

        public void LocationFromCurrentWorldUnlocked(params int[] locationIds)
        {
            _session.Locations.CompleteLocationChecks(locationIds);
        }

        public void SetIsPlayerInWorld(bool isInWorld)
        {
            _session.Socket.SendPacket(new StatusUpdatePacket()
            {
                Status = isInWorld
                    ? ArchipelagoClientState.ClientPlaying
                    : ArchipelagoClientState.ClientReady
            });
            isRaftWorldLoaded = isInWorld;
        }

        public void SendChatMessage(string message)
        {
            _session.Socket.SendPacket(new SayPacket()
            {
                Text = message
            });
        }

        public void HintForItem(string itemText)
        {
            // TODO How2hint, it's not anywhere in protocol =/ Probably read spoiler log in code and find item? Or something...
            Console.WriteLine(itemText);
        }

        public void Connect(string username, string password)
        {
            var invalidMethodNames = new List<string>();
            _addNameIfInvalid(RaftItemUnlockedForCurrentWorld, "RaftItemUnlockedForCurrentWorld", invalidMethodNames);
            _addNameIfInvalid(PrintMessage, "PrintMessage", invalidMethodNames);
            if (invalidMethodNames.Count > 0)
            {
                throw new InvalidOperationException($"Not all Proxy -> Raft events are set up. Set those up before connecting. ({string.Join(",", invalidMethodNames)})");
            }
            var loginResult = _session.TryConnectAndLogin("Raft", username, new Version(0, 3, 0), password: password);
            if (loginResult.Successful)
            {
                PrintMessage("Connected");
            }
            else
            {
                PrintMessage("Failed to connect");
            }
        }

        public int GetLocationIdFromName(string locationName)
        {
            return _session.Locations.GetLocationIdFromName("Raft", locationName);
        }

        public string GetItemNameFromId(int itemId)
        {
            return _session.Items.GetItemName(itemId);
        }

        public void Disconnect()
        {
            _session.Socket.Disconnect();
        }

        private void _addNameIfInvalid(object action, string name, List<string> addTo)
        {
            if (action == null)
            {
                addTo.Add(name);
            }
        }

        private void HandlePacket(ArchipelagoPacketBase packet)
        {
            PrintMessage($"Packet received: {packet.PacketType}");
            switch (packet.PacketType)
            {
                case ArchipelagoPacketType.RoomInfo:
                    var roomInfoPacket = (RoomInfoPacket)packet;
                    hintCost = roomInfoPacket.HintCost;
                    // TODO Handle forfeit, hint, etc logic? Or does Archipelago server handle it for us and throw errors at us?
                    break;
                case ArchipelagoPacketType.Say:
                    PrintMessage(((SayPacket)packet).Text);
                    break;
                case ArchipelagoPacketType.Connected:
                    SetIsPlayerInWorld(isRaftWorldLoaded); // TODO should we optimize this out, eg SendMultiplePackets() and a manual packet creation?
                    // TODO what to do with things like slotData
                    break;
                case ArchipelagoPacketType.ReceivedItems:
                    // Do nothing, we're handling elsewhere (but keep case here so we know to mark packet as handled)
                    break;
                case ArchipelagoPacketType.Print:
                    var printPacket = (PrintPacket)packet;
                    PrintMessage(printPacket.Text);
                    break;
                case ArchipelagoPacketType.PrintJSON:
                    var printJsonPacket = (PrintJsonPacket)packet;
                    StringBuilder build = new StringBuilder();
                    foreach (var messagePart in printJsonPacket.Data)
                    {
                        build.Append(GetStringForJsonPartData(messagePart));
                    }
                    PrintMessage(build.ToString()); // TODO Should we differentiate between Hints and Location Checks? Thinking no atm.
                    break;
                case ArchipelagoPacketType.RoomUpdate:
                    var roomUpdatePacket = (RoomUpdatePacket)packet;
                    hintCost = roomUpdatePacket.HintCost;
                    currentHintPoints = roomUpdatePacket.HintPoints;
                    break;
                default:
                    var pktTxt = $"Unknown packet: {JsonConvert.SerializeObject(packet)}";
                    PrintMessage(pktTxt);
                    break;
            }
            // TODO Implement other packets
        }

        private string GetStringForJsonPartData(JsonMessagePart data)
        {
            switch (data.Type)
            {
                case JsonMessagePartType.PlayerId:
                    return _session.Players.GetPlayerName(int.Parse(data.Text)); // TODO Error handling
                case JsonMessagePartType.ItemId:
                    return _session.Items.GetItemName(int.Parse(data.Text)); // TODO Error handling
                case JsonMessagePartType.LocationId:
                    return _session.Locations.GetLocationNameFromId(int.Parse(data.Text));
                case JsonMessagePartType.Color:
                    return ""; // TODO Color? Ignoring for now
                case JsonMessagePartType.PlayerName: // Explicitly calling out expected strings to just return data for
                case JsonMessagePartType.ItemName:
                case JsonMessagePartType.LocationName:
                case JsonMessagePartType.EntranceName:
                default:
                    return data.Text;
            }
        }
    }
}
