using Raftipelago.Network;
using System;
using System.Collections.Generic;

namespace Raftipelago.Data
{
    public class ArchipelagoDataManager
    {
        public Dictionary<int, string> ItemIdToName { get; set; }
        public Dictionary<int, string> PlayerIdToName { get; set; }

        public string GetItemName(int itemId)
        {
            if (Semih_Network.IsHost || Semih_Network.InSinglePlayerMode)
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
                return ItemIdToName.GetValueOrDefault(itemId, null);
            }
            return null;
        }

        public string GetPlayerName(int playerId)
        {
            if (Semih_Network.IsHost || Semih_Network.InSinglePlayerMode)
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
                return ComponentManager<ArchipelagoDataManager>.Value.PlayerIdToName.GetValueOrDefault(playerId, null);
            }
            return null;
        }
    }
}
