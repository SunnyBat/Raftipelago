using Raftipelago.Network;
using System;
using System.Collections.Generic;

namespace Raftipelago.Data
{
    public class ArchipelagoDataManager
    {
        public Dictionary<int, string> ItemIdToName { get; set; }
        public Dictionary<int, string> PlayerIdToName { get; set; }
        public Dictionary<string, object> SlotData { get; set; }

        public string GetItemName(int itemId)
        {
            if (Raft_Network.IsHost)
            {
                if (ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected())
                {
                    try
                    {
                        return ComponentManager<IArchipelagoLink>.Value.GetItemNameFromId(itemId);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            else
            {
                if (ItemIdToName != null)
                {
                    return ItemIdToName.GetValueOrDefault(itemId, null);
                }
                else
                {
                    UnityEngine.Debug.LogError($"ItemIdToName not properly synchronized. Item {itemId} will be swallowed.");
                }
            }
            return null;
        }

        public string GetPlayerName(int playerId)
        {
            if (Raft_Network.IsHost)
            {
                if (ComponentManager<IArchipelagoLink>.Value.IsSuccessfullyConnected())
                {
                    try
                    {
                        return ComponentManager<IArchipelagoLink>.Value.GetPlayerAlias(playerId);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            else
            {
                return PlayerIdToName.GetValueOrDefault(playerId, null);
            }
            return null;
        }

        public bool TryGetSlotData<T>(string key, out T obj)
        {
            Dictionary<string, object> dictionaryToRead;
            if (Raft_Network.IsHost)
            {
                dictionaryToRead = ComponentManager<IArchipelagoLink>.Value.GetLastLoadedSlotData();
            }
            else
            {
                dictionaryToRead = SlotData;
            }

            if (dictionaryToRead != null && dictionaryToRead.TryGetValue(key, out object outVal))
            {
                UnityEngine.Debug.Log($"TGSD: {outVal} ({outVal.GetType().FullName}");
                obj = (T)outVal;
                return true;
            }
            else
            {
                obj = default(T);
                return false;
            }
        }
    }
}
