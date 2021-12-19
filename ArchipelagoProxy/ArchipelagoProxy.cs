using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ArchipelagoProxy
{
    public class ArchipelagoProxy
    {
        // Events FROM Raft
        public event Action<bool> SetPlayerIsInWorld;
        public event Action<string> SendChatMessage;
        public event Action<int, string> LocationFromCurrentWorldUnlocked;

        // Events TO Raft
        public event Action<string> ChatMessageReceived;
        public event Action<int, string> RaftItemUnlockedForCurrentWorld;

        /// <summary>
        /// Mapped as (teamNumber)-(playerId) :: (playerAlias)
        /// </summary>
        private Dictionary<string, string> _playerIdToNameMap = new Dictionary<string, string>();
        private int _currentTeamNumber;
        private int _currentPlayerSlot;
        private readonly ArchipelagoSession _session;
        public ArchipelagoProxy(string urlToHost)
        {
            SetPlayerIsInWorld += isInWorld =>
            {
                _session.Socket.SendPacket(new StatusUpdatePacket()
                {
                    Status = isInWorld
                        ? ArchipelagoClientState.ClientPlaying
                        : ArchipelagoClientState.ClientReady
                });
            };
            SendChatMessage += message =>
            {
                _session.Socket.SendPacket(new SayPacket()
                {
                    Text = message
                });
            };
            LocationFromCurrentWorldUnlocked += (locationId, playerName) =>
            {
                Console.WriteLine($"Player {playerName} unlocked location {locationId}");
            };
            _session = ArchipelagoSessionFactory.CreateSession(urlToHost);
            _session.Socket.PacketReceived += packet =>
            {
                HandlePacket(packet);
            };
        }

        public void Connect(string username, string password)
        {
            if (ChatMessageReceived == null
                || RaftItemUnlockedForCurrentWorld == null)
            {
                throw new InvalidOperationException("Not all Proxy -> Raft events are set up. Set those up before connecting.");
            }
            _session.Socket.Connect(); // TODO How2connect async (it doesn't return a Task wtf)
            var connectPacket = new ConnectPacket();
            connectPacket.Name = username;
            connectPacket.Password = password;
            connectPacket.Uuid = new Guid().ToString(); // TODO Unique ID per session, per user, per world?
            connectPacket.Game = "Raft";
            connectPacket.Version = new Version(0, 2, 0);
            _session.Socket.SendPacket(connectPacket);

            // TODO Send ClientReady packet after connected
            // TODO Parse ReceivedItems packet and add to list when update received
        }

        private void HandlePacket(ArchipelagoPacketBase packet)
        {
            Console.WriteLine($"Packet received: {packet.PacketType}");
            switch (packet.PacketType)
            {
                case ArchipelagoPacketType.Say:
                    ChatMessageReceived(((SayPacket)packet).Text);
                    break;
                case ArchipelagoPacketType.Connected:
                    var connectedPacket = (ConnectedPacket)packet;
                    _session.Socket.SendPacket(new SyncPacket());
                    connectedPacket.Players.ForEach(player =>
                    {
                        var playerKey = GetPlayerIdString(player);
                        _playerIdToNameMap.Remove(playerKey);
                        _playerIdToNameMap.Add(playerKey, player.Alias);
                    });
                    _currentTeamNumber = connectedPacket.Team;
                    _currentPlayerSlot = connectedPacket.Slot;
                    // TODO what to do with things like slotData
                    break;
                case ArchipelagoPacketType.ReceivedItems:
                    var riPacket = (ReceivedItemsPacket)packet;
                    riPacket.Items.ForEach(rItem =>
                    {
                        // TODO Filter to just ones we care about
                        var foundByPlayer = _playerIdToNameMap.TryGetValue($"{_currentTeamNumber}-{rItem.Player}", out string playName)
                            ? playName
                            : "<UnknownPlayer>";
                        RaftItemUnlockedForCurrentWorld(rItem.Item, foundByPlayer);
                    });
                    break;
                default:
                    var pktTxt = $"Unknown packet: {JsonConvert.SerializeObject(packet)}";
                    ChatMessageReceived(pktTxt); // TODO Separate message? Maybe have it print in red or something? =/
                    Console.WriteLine(pktTxt);
                    break;
            }
            // TODO Implement other packets
        }
        
        private string GetPlayerIdString(NetworkPlayer player)
        {
            return $"{player.Team}-{player.Slot}";
        }
    }
}
