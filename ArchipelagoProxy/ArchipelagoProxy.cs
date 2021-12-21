using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

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

        // Queues for events
        // Events should be run on the main Unity thread. Thus, we queue up a heartbeat that runs on the
        // main Unity thread that will dequeue these objects and trigger the appropriate events. Because
        // of this, these queues need to be thread-safe.
        private ConcurrentQueue<NetworkItem> _itemReceivedQueue = new ConcurrentQueue<NetworkItem>();
        private ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

        public readonly ArchipelagoSession _session; // TODO Make private

        // Tracking between Archipelago Server <-> Proxy <-> Raft
        private bool isRaftWorldLoaded = false;
        public ArchipelagoProxy(string urlToHost)
        {
            _session = ArchipelagoSessionFactory.CreateSession(urlToHost);
            _session.Items.ItemReceived += itemHelper => // Called once per new item, don't loop dequeue (though it only enqueus one item at a time anyways)
            {
                try
                {
                    _itemReceivedQueue.Enqueue(itemHelper.DequeueItem());
                }
                catch (Exception e)
                {
                    _messageQueue.Enqueue(e.Message);
                }
            };
            _session.Socket.PacketReceived += packet =>
            {
                HandlePacket(packet);
            };
        }

        public void Heartbeat()
        {
            if (PrintMessage != null)
            {
                while (_messageQueue.TryDequeue(out string nextMessage))
                {
                    PrintMessage(nextMessage);
                }
            }
            if (RaftItemUnlockedForCurrentWorld != null)
            {
                if (isRaftWorldLoaded) // Only run these once we've successfully loaded a world
                {
                    while (_itemReceivedQueue.TryDequeue(out NetworkItem res))
                    {
                        RaftItemUnlockedForCurrentWorld(res.Item, _session.Players.GetPlayerAlias(res.Player));
                    }
                }
            }
        }

        public void LocationFromCurrentWorldUnlocked(params int[] locationIds)
        {
            _session.Locations.CompleteLocationChecks(locationIds);
        }

        public int[] GetAllLocationIdsUnlockedForCurrentWorld()
        {
            return _session.Locations.AllLocationsChecked.ToArray();
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
            var loginResult = _session.TryConnectAndLogin("Raft", username, new Version(0, 5, 0), password: password);
            if (loginResult.Successful)
            {
                _messageQueue.Enqueue("Connected");
            }
            else
            {
                _messageQueue.Enqueue("Failed to connect");
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
            _messageQueue.Enqueue($"Packet received: {packet.PacketType}");
            switch (packet.PacketType)
            {
                case ArchipelagoPacketType.Say:
                    _messageQueue.Enqueue(((SayPacket)packet).Text);
                    break;
                case ArchipelagoPacketType.Print:
                    _messageQueue.Enqueue(((PrintPacket)packet).Text);
                    break;
                case ArchipelagoPacketType.PrintJSON:
                    StringBuilder build = new StringBuilder();
                    foreach (var messagePart in ((PrintJsonPacket)packet).Data)
                    {
                        build.Append(GetStringForJsonPartData(messagePart));
                    }
                    _messageQueue.Enqueue(build.ToString()); // TODO Should we differentiate between Hints and Location Checks? Thinking no atm.
                    break;
                case ArchipelagoPacketType.RoomInfo:
                case ArchipelagoPacketType.RoomUpdate:
                case ArchipelagoPacketType.Connected:
                case ArchipelagoPacketType.ReceivedItems:
                    // Do nothing, we're handling elsewhere (but keep case here so we know to mark packet as handled)
                    break;
                default:
                    _messageQueue.Enqueue($"Unknown packet: {JsonConvert.SerializeObject(packet)}");
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
