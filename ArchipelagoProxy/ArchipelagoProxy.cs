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
using System.Text.RegularExpressions;
using System.Threading;

namespace ArchipelagoProxy
{
    public class ArchipelagoProxy
    {
        public const int ArchipelagoDataVersion = 1;

        private readonly Regex PortFinderRegex = new Regex(@":(\d+)");

        // Events TO Raft
        /// <summary>
        /// Called when successfully connected to server
        /// </summary>
        private event Action ConnectedToServer;
        /// <summary>
        /// Called when a new Raft item is unlocked for the current world.
        /// </summary>
        private event Action<int, int, int> RaftItemUnlockedForCurrentWorld;
        /// <summary>
        /// Called when a message is received. This can be a chat message, an item received message, etc
        /// </summary>
        private event Action<string> PrintMessage;
        /// <summary>
        /// Called for debug events. Not for general user consumption.
        /// </summary>
        private event Action<string> DebugMessage;

        // Queues for events
        // Events should be run on the main Unity thread. Thus, we queue up a heartbeat that runs on the
        // main Unity thread that will dequeue these objects and trigger the appropriate events. Because
        // of this, these queues need to be thread-safe.
        private ConcurrentQueue<int> _locationUnlockQueue = new ConcurrentQueue<int>();
        private ConcurrentQueue<NetworkItem> _itemReceivedQueue = new ConcurrentQueue<NetworkItem>();
        private ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _debugQueue = new ConcurrentQueue<string>();

        // Lock for all non-thread-safe objects
        private readonly object LockForClass = new object();

        // Everything here should be gated behind the LockForClass, and should never spin on it
        private readonly ArchipelagoSession _session;

        // Tracking between Archipelago Server <-> Proxy <-> Raft
        private bool isRaftWorldLoaded = false;
        private bool isSuccessfullyConnected = false;
        private bool isGameCompleted = false; // Edge case of not connected, complete game, connect
        private bool triggeredConnectedAction = false;
        public ArchipelagoProxy(string urlToHost)
        {
            if (urlToHost.Contains(":"))
            {
                var indexOfColon = urlToHost.IndexOf(":");
                var hostAddress = urlToHost.Substring(0, indexOfColon); // Assumes no additional path is required in URL; if there is, we need to slice :port out instead
                var portStr = PortFinderRegex.Match(urlToHost).Groups[1].Value;
                if (int.TryParse(portStr, out int port))
                {
                    _session = ArchipelagoSessionFactory.CreateSession(urlToHost);
                }
                else
                {
                    throw new ArgumentException("Invalid URL for Archipelago: " + urlToHost);
                }
            }
            else
            {
                _session = ArchipelagoSessionFactory.CreateSession(urlToHost);
            }
            _session.Items.ItemReceived += itemHelper =>
            {
                try
                {
                    while (itemHelper.Any()) // Generally will only be one but might as well loop
                    {
                        _itemReceivedQueue.Enqueue(itemHelper.DequeueItem());
                    }
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
            _session.Socket.SocketClosed += closedEventArgs =>
            {
                lock (LockForClass)
                {
                    isSuccessfullyConnected = false;
                    triggeredConnectedAction = false;
                }
                if (!closedEventArgs.WasClean)
                {
                    _messageQueue.Enqueue($"Disconnected from server with reason \"{closedEventArgs.Reason}\" ({closedEventArgs.Code})");
                }
                else
                {
                    _debugQueue.Enqueue($"Disconnected from server.");
                }
            };
        }

        public bool IsSuccessfullyConnected()
        {
            lock (LockForClass)
            {
                return isSuccessfullyConnected;
            }
        }

        public void RequeueAllItems()
        {
            lock(LockForClass)
            {
                if (isSuccessfullyConnected)
                {
                    foreach (var item in _session.Items.AllItemsReceived)
                    {
                        _itemReceivedQueue.Enqueue(item);
                    }
                }
            }
        }

        public void Heartbeat()
        {
            while (_messageQueue.TryDequeue(out string nextMessage))
            {
                PrintMessage(nextMessage);
            }
            lock (LockForClass)
            {
                if (IsSuccessfullyConnected()) // Don't process most things if we're not properly connected -- we don't want to accidentally send invalid data
                {
                    if (DebugMessage != null) // Not required to run
                    {
                        while (_debugQueue.TryDequeue(out string nextMessage))
                        {
                            DebugMessage(nextMessage);
                        }
                    }
                    if (isRaftWorldLoaded) // Only run these once we've successfully loaded a world
                    {
                        while (_itemReceivedQueue.TryDequeue(out NetworkItem res))
                        {
                            RaftItemUnlockedForCurrentWorld(res.Item, res.Location, res.Player);
                        }
                    }
                    if (!triggeredConnectedAction)
                    {
                        triggeredConnectedAction = true; // Set first in case ConnectedToServer() blows up
                        ConnectedToServer();
                    }
                }
            }
        }

        public void AddConnectedToServerEvent(Action newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    ConnectedToServer += newEvent;
                }
            }
        }

        public void AddRaftItemUnlockedForCurrentWorldEvent(Action<int, int, int> newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    RaftItemUnlockedForCurrentWorld += newEvent;
                }
            }
        }

        public void AddPrintMessageEvent(Action<string> newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    PrintMessage += newEvent;
                }
            }
        }

        public void AddDebugMessageEvent(Action<string> newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    DebugMessage += newEvent;
                }
            }
        }

        public void SetGameCompleted(bool completed)
        {
            lock (LockForClass)
            {
                isGameCompleted = completed;
            }

            if (completed && IsSuccessfullyConnected())
            {
                _session.Socket.SendPacket(new StatusUpdatePacket()
                {
                    Status = ArchipelagoClientState.ClientGoal
                });
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

        public void SetIsPlayerInWorld(bool isInWorld, bool forceResync = false)
        {
            bool gameCompleted;
            lock (LockForClass)
            {
                isRaftWorldLoaded = isInWorld;
                gameCompleted = isGameCompleted;
            }

            if (IsSuccessfullyConnected())
            {
                _session.Socket.SendPacket(new StatusUpdatePacket()
                {
                    Status = isInWorld
                        ? gameCompleted
                            ? ArchipelagoClientState.ClientGoal
                            : ArchipelagoClientState.ClientPlaying
                        : ArchipelagoClientState.ClientReady
                });
            }
        }

        public void SendChatMessage(string message)
        {
            if (IsSuccessfullyConnected())
            {
                _session.Socket.SendPacket(new SayPacket()
                {
                    Text = message
                });
            }
        }

        public void Connect(string username, string password)
        {
            var invalidMethodNames = new List<string>();
            _addNameIfInvalid(ConnectedToServer, "ConnectedToServer", invalidMethodNames);
            _addNameIfInvalid(RaftItemUnlockedForCurrentWorld, "RaftItemUnlockedForCurrentWorld", invalidMethodNames);
            _addNameIfInvalid(PrintMessage, "PrintMessage", invalidMethodNames);
            if (invalidMethodNames.Count > 0)
            {
                throw new InvalidOperationException($"Not all Proxy -> Raft events are set up. Set those up before connecting. ({string.Join(",", invalidMethodNames)})");
            }
            var loginResult = _session.TryConnectAndLogin("Raft", username, new Version(0, ArchipelagoDataVersion, 0), password: password);
            if (loginResult.Successful)
            {
                _messageQueue.Enqueue("Successfully connected to Archipelago");
            }
            else
            {
                _messageQueue.Enqueue("Failed to connect to Archipelago");
                try
                {
                    _session.Socket.Disconnect();
                }
                catch (Exception) { }
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

        public string GetPlayerAlias(int playerId)
        {
            return _session.Players.GetPlayerAlias(playerId);
        }

        public void Disconnect()
        {
            try
            {
                _session.Socket.Disconnect();
            }
            catch (Exception)
            {
            }
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
            _debugQueue.Enqueue($"Packet received: {JsonConvert.SerializeObject(packet)}");
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
                    _messageQueue.Enqueue(build.ToString());
                    break;
                case ArchipelagoPacketType.Connected:
                    lock (LockForClass)
                    {
                        isSuccessfullyConnected = true;
                        SetIsPlayerInWorld(isRaftWorldLoaded, true); // Call so we trigger all required processes
                    }
                    List<int> allLocations = new List<int>();
                    while (_locationUnlockQueue.TryDequeue(out int nextLocation))
                    {
                        allLocations.Add(nextLocation);
                    }
                    if (allLocations.Count > 0)
                    {
                        LocationFromCurrentWorldUnlocked(allLocations.ToArray());
                    }
                    break;
                case ArchipelagoPacketType.ConnectionRefused:
                    var connectionRefusedPacket = (ConnectionRefusedPacket)packet;
                    foreach (var err in connectionRefusedPacket.Errors)
                    {
                        switch (err)
                        {
                            case ConnectionRefusedError.InvalidSlot:
                                _messageQueue.Enqueue("Error connecting: Username not found.");
                                break;
                            case ConnectionRefusedError.InvalidGame:
                                _messageQueue.Enqueue("Error connecting: Invalid game. Server likely does not support Raft.");
                                break;
                            case ConnectionRefusedError.InvalidPassword:
                                _messageQueue.Enqueue("Error connecting: Invalid password.");
                                break;
                            case ConnectionRefusedError.SlotAlreadyTaken:
                                _messageQueue.Enqueue("Error connecting: Someone else is already connected with this username (slot already taken).");
                                break;
                            case ConnectionRefusedError.IncompatibleVersion:
                                _messageQueue.Enqueue("Error connecting: Incompatible versions between Archipelago and Raftipelago.");
                                break;
                            default:
                                _messageQueue.Enqueue("Error connecting: Unknown reason.");
                                break;
                        }
                    }
                    break;
                case ArchipelagoPacketType.RoomInfo:
                case ArchipelagoPacketType.RoomUpdate:
                case ArchipelagoPacketType.ReceivedItems:
                case ArchipelagoPacketType.DataPackage:
                    // Do nothing, we're handling elsewhere (but keep case here so we know to mark packet as handled)
                    break;
                default:
                    _debugQueue.Enqueue($"Unknown packet: {JsonConvert.SerializeObject(packet)}");
                    break;
            }
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
