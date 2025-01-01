using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Newtonsoft.Json;
using RaftipelagoTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace ArchipelagoProxy
{
    public class ArchipelagoProxy : MarshalByRefObject
    {
        private const int TIME_BETWEEN_UPDATES_MS = 100;
        private const int TIME_BETWEEN_RECONNECTS = 2500;
        private const int MAX_RECONNECT_ATTEMPTS = 5;
        private const string DEATH_LINK_TAG = "DeathLink";

        private readonly Regex PortFinderRegex = new Regex(@":(\d+)");

        private readonly ArchipelagoSession _session;
        private readonly Thread _commsThread;

        // Lock for all non-thread-safe objects
        // We use a private object so we don't have to worry about other threads locking this ArchipelagoProxy instance object
        private readonly object LockForClass = new object();

        private DeathLinkService _deathLink;
        private ActionHandler _deathLinkHandlerFromClient;

        // === Server -> Client Events ===

        /// <summary>
        /// Called when successfully connected to server. Must only be called from main Unity thread.
        /// </summary>
        private event Action ConnectedToServer;
        /// <summary>
        /// Called when a new Raft item is unlocked for the current world. Must only be called from main Unity thread.
        /// </summary>
        private event Action<long, long, int, int> RaftItemUnlockedForCurrentWorld;
        /// <summary>
        /// Called for error events. These are errors that should be communicated to the user. Must only be called from main Unity thread.
        /// </summary>
        private event Action<string> ErrorMessage;
        /// <summary>
        /// Called when a message is received. This can be a chat message, an item received message, etc. Must only be called from main Unity thread.
        /// </summary>
        private event Action<string> PrintMessage;
        /// <summary>
        /// Called for debug events. Not for general user consumption. Must only be called from main Unity thread.
        /// </summary>
        private event Action<string> DebugMessage;

        // Queues for events
        // Events should be run on the main Unity thread. Thus, we queue up a heartbeat that runs on the
        // main Unity thread that will dequeue these objects and trigger the appropriate events. Because
        // of this, these queues need to be thread-safe.
        private ConcurrentQueue<ItemInfo> _itemReceivedQueue = new ConcurrentQueue<ItemInfo>();
        private ConcurrentQueue<string> _errorQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _debugQueue = new ConcurrentQueue<string>();

        private bool _triggeredConnectedAction = false;

        // === Client -> Server Events ===
        // We use these so the main Unity thread doesn't block while we're sending packets.
        // Instead, we queue up everything we need, then flush them every so often on
        // a separate thread.
        private ConcurrentQueue<long> _locationUnlockQueue = new ConcurrentQueue<long>();
        private ConcurrentQueue<string> _chatQueue = new ConcurrentQueue<string>();

        private bool _isGameCompleted = false; // Edge case of not connected, complete game, connect
        /// <summary>
        /// If this is not the same as isRaftWorldLoaded, we must resend the state.
        /// </summary>
        private ArchipelagoClientState _lastSentRaftWorldState = ArchipelagoClientState.ClientUnknown;

        // === Server <-> Proxy <-> Client ===

        // Tracking between Archipelago Server <-> Proxy <-> Raft
        // Everything here should be gated behind the LockForClass, and should never spin on it
        private bool _isRaftWorldLoaded = false;
        private bool _isSuccessfullyConnected = false;
        private int _successiveConnectFailures = MAX_RECONNECT_ATTEMPTS;
        private string _lastUsedUsername;
        private string _lastUsedPassword;
        private bool _isUserIssuedConnect = false;
        private bool _shouldDisconnect = false;
        private bool _shouldKeepRunning = true;
        private bool _deathLinkReceived = false;
        private Dictionary<string, object> _slotData;
        private int _receivedItemCount = 0;

        public ArchipelagoProxy(string urlToHost)
        {
            if (urlToHost.Contains(":"))
            {
                var indexOfColon = urlToHost.LastIndexOf(":");
                var hostAddress = urlToHost.Substring(0, indexOfColon); // Assumes no additional path is required in URL; if there is, we need to slice :port out instead
                var portStr = PortFinderRegex.Match(urlToHost).Groups[1].Value;
                if (int.TryParse(portStr, out int port))
                {
                    _session = ArchipelagoSessionFactory.CreateSession(hostAddress, port);
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
                    _debugQueue.Enqueue("ItemHelper processing");
                    while (itemHelper.Any()) // Generally will only be one but might as well loop
                    {
                        _debugQueue.Enqueue("ItemHelper received item");
                        _itemReceivedQueue.Enqueue(itemHelper.DequeueItem());
                    }
                }
                catch (Exception e)
                {
                    _errorQueue.Enqueue(e.Message);
                }
            };
            _session.MessageLog.OnMessageReceived += receivedMessage =>
            {
                _messageQueue.Enqueue(receivedMessage.ToString());
            };
            _session.Socket.PacketReceived += packet =>
            {
                HandlePacket(packet);
            };
            _session.Socket.SocketOpened += () =>
            {
                lock (LockForClass)
                {
                    _successiveConnectFailures = 0;
                }
            };
            _session.Socket.SocketClosed += closedReason =>
            {
                lock (LockForClass)
                {
                    _isSuccessfullyConnected = false;
                    _triggeredConnectedAction = false;
                }
                if (!string.IsNullOrWhiteSpace(closedReason))
                {
                    _messageQueue.Enqueue($"Disconnected from server with reason \"{closedReason}\"");
                }
            };
            _commsThread = new Thread(new ThreadStart(_runCommsThread));
            _commsThread.Start();
        }

        // https://social.msdn.microsoft.com/Forums/en-US/3ab17b40-546f-4373-8c08-f0f072d818c9/remotingexception-when-raising-events-across-appdomains?forum=netfxremoting
        // This will make this object stick around forever. This can be a memory leak, however we're going to make these so infrequently that any orphaned instances
        // should be inconsequential.
        public override object InitializeLifetimeService()
        {
            return null;
        }

        public bool IsSuccessfullyConnected()
        {
            lock (LockForClass)
            {
                return _isSuccessfullyConnected;
            }
        }

        public void IrreversablyDestroy()
        {
            lock (LockForClass)
            {
                _shouldKeepRunning = false;
            }
        }

        public void RequeueAllItems()
        {
            lock (LockForClass)
            {
                if (_isSuccessfullyConnected)
                {
                    _receivedItemCount = 0; // Reset item count
                    foreach (var item in _session.Items.AllItemsReceived)
                    {
                        _itemReceivedQueue.Enqueue(item);
                    }
                }
            }
        }

        public object[] GetAllReceivedItems()
        {
            List<long> itemIds = new List<long>();
            List<long> locationIds = new List<long>();
            List<int> playerIds = new List<int>();
            List<int> itemIndeces = new List<int>();
            for (int i = 0; i < _session.Items.AllItemsReceived.Count; i++)
            {
                var item = _session.Items.AllItemsReceived[i];
                itemIds.Add(item.ItemId);
                locationIds.Add(item.LocationId);
                playerIds.Add(item.Player);
                itemIndeces.Add(i);
            }
            return new object[] { itemIds, locationIds, playerIds, itemIndeces };
        }

        public void Heartbeat()
        {
            while (_errorQueue.TryDequeue(out string nextMessage))
            {
                ErrorMessage(nextMessage);
            }
            while (_messageQueue.TryDequeue(out string nextMessage))
            {
                PrintMessage(nextMessage);
            }
            if (DebugMessage != null) // Not required to run
            {
                while (_debugQueue.TryDequeue(out string nextMessage))
                {
                    DebugMessage(nextMessage);
                }
            }
            bool successfullyConnected;
            bool isWorldLoaded;
            bool triggeredConnectedAction;
            bool deathLinkReceived;
            lock (LockForClass)
            {
                successfullyConnected = IsSuccessfullyConnected(); // Call in lock to prevent releasing then immediately reclaiming lock
                isWorldLoaded = _isRaftWorldLoaded;
                triggeredConnectedAction = _triggeredConnectedAction;
                deathLinkReceived = _deathLinkReceived;
            }
            if (successfullyConnected) // Don't process most things if we're not properly connected -- we don't want to accidentally send invalid data
            {
                if (isWorldLoaded) // Only run these once we've successfully loaded a world
                {
                    while (_itemReceivedQueue.TryDequeue(out ItemInfo res))
                    {
                        RaftItemUnlockedForCurrentWorld(res.ItemId, res.LocationId, res.Player, ++_receivedItemCount);
                    }
                    if (deathLinkReceived && _deathLinkHandlerFromClient != null)
                    {
                        _deathLinkHandlerFromClient.Invoke();
                        lock (LockForClass)
                        {

                            _deathLinkReceived = false;
                        }
                    }
                }
                if (!triggeredConnectedAction)
                {
                    lock (LockForClass)
                    {
                        _triggeredConnectedAction = true; // Set first in case ConnectedToServer() blows up
                    }
                    ConnectedToServer();
                }
            }
        }

        public void AddConnectedToServerEvent(ActionHandler newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    ConnectedToServer += () => newEvent.Invoke();
                }
            }
        }

        public void AddRaftItemUnlockedForCurrentWorldEvent(QuadroupleArgumentActionHandler<long, long, int, int> newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    RaftItemUnlockedForCurrentWorld += (long arg1, long arg2, int arg3, int arg4) => newEvent.Invoke(arg1, arg2, arg3, arg4);
                }
            }
        }

        public void AddErrorMessageEvent(SingleArgumentActionHandler<string> newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    ErrorMessage += (string arg1) => newEvent.Invoke(arg1);
                }
            }
        }

        public void AddPrintMessageEvent(SingleArgumentActionHandler<string> newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    PrintMessage += (string arg1) => newEvent.Invoke(arg1);
                }
            }
        }

        public void AddDebugMessageEvent(SingleArgumentActionHandler<string> newEvent)
        {
            if (newEvent != null)
            {
                lock (LockForClass)
                {
                    DebugMessage += (string arg1) => newEvent.Invoke(arg1);
                }
            }
        }

        public void SetGameCompleted(bool completed)
        {
            lock (LockForClass)
            {
                _isGameCompleted = completed;
                _lastSentRaftWorldState = ArchipelagoClientState.ClientUnknown; // Force resend of world state
            }
        }

        public void LocationFromCurrentWorldUnlocked(params long[] locationIds)
        {
            foreach (var locId in locationIds)
            {
                _locationUnlockQueue.Enqueue(locId);
            }
        }

        public long[] GetAllLocationIdsUnlockedForCurrentWorld()
        {
            return _session.Locations.AllLocationsChecked.ToArray();
        }

        public void SetIsPlayerInWorld(bool isInWorld, bool forceResync = false)
        {
            bool gameCompleted;
            lock (LockForClass)
            {
                _isRaftWorldLoaded = isInWorld;
                gameCompleted = _isGameCompleted;
            }
        }

        public void SendChatMessage(string message)
        {
            _chatQueue.Enqueue(message);
        }

        public void Connect(string username, string password)
        {
            var invalidMethodNames = new List<string>();
            _addNameIfInvalid(ConnectedToServer, "ConnectedToServer", invalidMethodNames);
            _addNameIfInvalid(RaftItemUnlockedForCurrentWorld, "RaftItemUnlockedForCurrentWorld", invalidMethodNames);
            _addNameIfInvalid(ErrorMessage, "ErrorMessage", invalidMethodNames);
            _addNameIfInvalid(PrintMessage, "PrintMessage", invalidMethodNames);
            if (invalidMethodNames.Count > 0)
            {
                throw new InvalidOperationException($"Not all Proxy -> Raft events are set up. Set those up before connecting. ({string.Join(",", invalidMethodNames)})");
            }

            lock (LockForClass)
            {
                _lastUsedUsername = username;
                _lastUsedPassword = password;
                _isUserIssuedConnect = true;
                _successiveConnectFailures = MAX_RECONNECT_ATTEMPTS - 1; // Do not attempt to reconnect if this fails
            }
        }

        public long GetLocationIdFromName(string locationName)
        {
            return _session.Locations.GetLocationIdFromName("Raft", locationName);
        }

        public string GetItemNameFromId(long itemId)
        {
            return _session.Items.GetItemName(itemId);
        }

        public string GetPlayerAlias(int playerId)
        {
            return _session.Players.GetPlayerAlias(playerId);
        }

        public Dictionary<string, object> GetLastLoadedSlotData()
        {
            return _slotData;
        }

        public void Disconnect()
        {
            try
            {
                lock (LockForClass)
                {
                    _successiveConnectFailures = MAX_RECONNECT_ATTEMPTS; // Do not attempt to reconnect
                    _shouldDisconnect = true;
                }
            }
            catch (Exception)
            {
                _messageQueue.Enqueue("Error occurred while disconnecting from server. You may have already been disconnected.");
            }
        }

        public void AddDeathLinkHandler(ActionHandler deathLinkHandler)
        {
            if (deathLinkHandler != null)
            {
                lock (LockForClass)
                {
                    _deathLinkHandlerFromClient = deathLinkHandler;
                    if (_deathLink != null)
                    {
                        _deathLink.OnDeathLinkReceived += _handleDeathLink;
                    }
                }
            }
        }

        public void SendDeathLinkIfNecessary(string cause)
        {
            try
            {
                if (_deathLink != null && IsSuccessfullyConnected() && _slotData.TryGetValue(DEATH_LINK_TAG, out object isDeathLinkEnabled) && ((bool)isDeathLinkEnabled)
                    && !string.IsNullOrWhiteSpace(cause))
                {
                    _deathLink.SendDeathLink(new DeathLink(_session.Players.GetPlayerName(_session.ConnectionInfo.Slot), cause));
                }
            }
            catch (Exception e)
            {
                _errorQueue.Enqueue("Could not send DeathLink: " + e);
            }
        }

        private void _runCommsThread()
        {
            bool shouldKeepRunning;
            lock (LockForClass)
            {
                shouldKeepRunning = _shouldKeepRunning;
            }
            while (shouldKeepRunning)
            {
                var startTime = DateTime.Now;
                try
                {
                    if (_comms_connect())
                    {
                        _comms_updateRaftWorldState();
                        _comms_sendLocations();
                        _comms_sendChat();
                    }
                }
                catch (Exception e)
                {
                    _errorQueue.Enqueue(e.ToString());
                }

                _sleepForMillis(startTime, TIME_BETWEEN_UPDATES_MS);
                lock (LockForClass)
                {
                    shouldKeepRunning = _shouldKeepRunning;
                }
            }
        }

        private bool _comms_connect()
        {
            int successiveFailures;
            string username;
            string password;
            bool isUserIssued;
            bool shouldDisconnect;
            lock (LockForClass) // Don't block lock on reconnect
            {
                successiveFailures = ++_successiveConnectFailures;
                username = _lastUsedUsername;
                password = _lastUsedPassword;
                isUserIssued = _isUserIssuedConnect;
                shouldDisconnect = _shouldDisconnect;
            }

            bool isConnected = false;
            try
            {
                isConnected = _session.Socket.Connected;
            }
            catch
            {
                // Not sure why it's still doing this, I've verified the DLL has the correct code... Default to false =/
            }
            if (isConnected && shouldDisconnect)
            {
                _session.Socket.Disconnect();
                _messageQueue.Enqueue("Disconnected from server.");
                lock (LockForClass)
                {
                    _isSuccessfullyConnected = false;
                    _triggeredConnectedAction = false;
                }
                return false;
            }
            else if (!isConnected)
            {
                while (!isConnected && successiveFailures <= MAX_RECONNECT_ATTEMPTS)
                {
                    var startTime = DateTime.Now;
                    if (!isUserIssued)
                    {
                        _messageQueue.Enqueue($"Attempting to reconnect from server (#" + successiveFailures + ")");
                    }
                    isConnected = _connectInternal(username, password, isUserIssued);
                    lock (LockForClass)
                    {
                        if (isConnected)
                        {
                            _successiveConnectFailures = 0; // Auto-attempt to reconnect when disconnected
                        }
                        else
                        {
                            successiveFailures = ++_successiveConnectFailures;
                        }
                    }
                    if (!isConnected)
                    {
                        _sleepForMillis(startTime, TIME_BETWEEN_RECONNECTS);
                    }
                }
                lock (LockForClass)
                {
                    _isUserIssuedConnect = false;
                }
            }
            return isConnected;
        }

        private void _comms_updateRaftWorldState()
        {
            ArchipelagoClientState lastSentState;
            bool worldLoaded;
            bool goalCompleted;
            lock (LockForClass)
            {
                lastSentState = _lastSentRaftWorldState;
                worldLoaded = _isRaftWorldLoaded;
                goalCompleted = _isGameCompleted;
            }
            var currentState = worldLoaded
                ? goalCompleted
                    ? ArchipelagoClientState.ClientGoal
                    : ArchipelagoClientState.ClientPlaying
                : ArchipelagoClientState.ClientReady;
            if (currentState != lastSentState)
            {
                _session.Socket.SendPacket(new StatusUpdatePacket()
                {
                    Status = currentState
                });
            }
        }

        private void _comms_sendLocations()
        {
            List<long> allLocations = new List<long>();
            while (_locationUnlockQueue.TryDequeue(out long nextLocation))
            {
                allLocations.Add(nextLocation);
            }
            if (allLocations.Count > 0)
            {
                _session.Locations.CompleteLocationChecks(allLocations.ToArray());
            }
        }

        private void _comms_sendChat()
        {
            while (_chatQueue.TryDequeue(out string nextMessage))
            {
                _session.Socket.SendPacket(new SayPacket()
                {
                    Text = nextMessage
                });
            }
        }

        private bool _connectInternal(string username, string password, bool disconnectOnFailure)
        {
            lock (LockForClass)
            {
                _lastUsedUsername = username;
                _lastUsedPassword = password;
            }
            var loginResult = _session.TryConnectAndLogin("Raft", username, ItemsHandlingFlags.AllItems, version: new Version(0, 3, 4), password: password);
            if (loginResult.Successful)
            {
                _messageQueue.Enqueue("Successfully connected to Archipelago");
                _slotData = ((LoginSuccessful)loginResult).SlotData;
                try
                {
                    if (_slotData.TryGetValue(DEATH_LINK_TAG, out object isDeathLinkEnabled) && ((bool)isDeathLinkEnabled))
                    {
                        var deathLinkService = _session.CreateDeathLinkService();
                        lock (LockForClass)
                        {
                            _deathLink = deathLinkService;
                            if (_deathLinkHandlerFromClient != null)
                            {
                                _deathLink.OnDeathLinkReceived += _handleDeathLink;
                            }
                        }
                        _deathLink.EnableDeathLink();
                    }
                }
                catch (Exception ex)
                {
                    _errorQueue.Enqueue("Unable to enable DeathLink: " + ex.Message);
                }
                return true;
            }
            else if (disconnectOnFailure)
            {
                _errorQueue.Enqueue("Failed to connect to Archipelago");
                try
                {
                    _session.Socket.Disconnect();
                }
                catch (Exception) { }
            }
            // else ignore

            return false;
        }

        private void _sleepForMillis(DateTime startTime, int durationInMillis)
        {
            var timeTakenToRun = (int)(DateTime.Now - startTime).TotalMilliseconds;
            var timeToSleep = Math.Max(0, durationInMillis - timeTakenToRun);
            Thread.Sleep(timeToSleep);
        }

        private void _handleDeathLink(DeathLink deathLinkObject)
        {
            try
            {
                if (_deathLinkHandlerFromClient != null && _slotData.TryGetValue(DEATH_LINK_TAG, out object isDeathLinkEnabled) && ((bool)isDeathLinkEnabled))
                {
                    lock (LockForClass)
                    {
                        _deathLinkReceived = true;
                    }
                    _chatQueue.Enqueue($"{deathLinkObject.Source} died to {deathLinkObject.Cause}");
                }
            }
            catch (Exception e)
            {
                _errorQueue.Enqueue("Could not handle death link: " + e.Message);
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
                case ArchipelagoPacketType.Connected:
                    lock (LockForClass)
                    {
                        _isSuccessfullyConnected = true;
                        SetIsPlayerInWorld(_isRaftWorldLoaded, true); // Call so we trigger all required processes
                    }
                    break;
                case ArchipelagoPacketType.ConnectionRefused:
                    lock (LockForClass)
                    {
                        // Do not attempt to reconnect
                        _successiveConnectFailures = MAX_RECONNECT_ATTEMPTS;
                    }
                    var connectionRefusedPacket = (ConnectionRefusedPacket)packet;
                    foreach (var err in connectionRefusedPacket.Errors)
                    {
                        switch (err)
                        {
                            case ConnectionRefusedError.InvalidSlot:
                                _errorQueue.Enqueue("Error connecting: Username not found.");
                                break;
                            case ConnectionRefusedError.InvalidGame:
                                _errorQueue.Enqueue("Error connecting: Invalid game. Server likely does not support Raft.");
                                break;
                            case ConnectionRefusedError.InvalidPassword:
                                _errorQueue.Enqueue("Error connecting: Invalid password.");
                                break;
                            case ConnectionRefusedError.SlotAlreadyTaken:
                                _errorQueue.Enqueue("Error connecting: Someone else is already connected with this username (slot already taken).");
                                break;
                            case ConnectionRefusedError.IncompatibleVersion:
                                _errorQueue.Enqueue("Error connecting: Incompatible versions between Archipelago and Raftipelago.");
                                break;
                            default:
                                _errorQueue.Enqueue("Error connecting: Unknown reason.");
                                break;
                        }
                    }
                    break;
                case ArchipelagoPacketType.PrintJSON:
                case ArchipelagoPacketType.RoomInfo:
                case ArchipelagoPacketType.RoomUpdate:
                case ArchipelagoPacketType.ReceivedItems:
                case ArchipelagoPacketType.DataPackage:
                    // Do nothing, we're handling elsewhere (but keep case here so we know to mark packet as handled)
                    break;
                case ArchipelagoPacketType.Bounced:
                    var convertedPacket = (BouncedPacket)packet;
                    if (convertedPacket.Tags != null && convertedPacket.Tags.Contains(DEATH_LINK_TAG)) // Ignore DeathLink packets
                    {
                        break;
                    }
                    goto default;
                default:
                    _debugQueue.Enqueue($"Unknown packet: {JsonConvert.SerializeObject(packet)}");
                    break;
            }
        }
    }
}
