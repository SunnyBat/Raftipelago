using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago.Data
{
    public class ItemMap
    {
        public int archipelagoLocationId { get; set; }
        public int raftUniqueIndex { get; set; }
    }

    public class ItemMapping
    {
        private Dictionary<int, int> _archipelagoToRaftIds = new Dictionary<int, int>();
        private Dictionary<int, int> _raftToArchipelagoIds = new Dictionary<int, int>();

        public ItemMapping()
        {
            // TODO Pull from Archipelago server dynamically? Or just keep same list as is in Archipelago server.
            ItemMap[] values = JsonConvert.DeserializeObject<ItemMap[]>(ComponentManager<EmbeddedFileUtils>.Value.ReadTextFile("Data", "ItemMapping.json"));
            foreach (var mapping in values)
            {
                _archipelagoToRaftIds.Add(mapping.archipelagoLocationId, mapping.raftUniqueIndex);
                _raftToArchipelagoIds.Add(mapping.raftUniqueIndex, mapping.archipelagoLocationId);
            }
        }

        /// <summary>
        /// Takes an Archipelago locationId and returns the Raft uniqueIndex associated with that item. This is NOT
        /// world-specific, but rather deals with Raft's item mapping to Archipelago's unique Location IDs. In other words,
        /// the uniqueIndex returned by this is the default item for this locationId in a non-randomized setting. This only
        /// works for locationIds specific to Raft.
        /// </summary>
        /// <param name="archipelagoLocationId"></param>
        /// <returns></returns>
        public int getRaftUniqueIndex(int archipelagoLocationId)
        {
            return _archipelagoToRaftIds.GetValueOrDefault(archipelagoLocationId, -1);
        }

        /// <summary>
        /// Takes a Raft item's uniqueIndex and returns the Archipelago locationId associated with that item. This is NOT
        /// world-specific, but rather deals with Raft's item mapping to Archipelago's unique Location IDs. In other words,
        /// the locationId returned by this is the default location for this raft item in a non-randomized setting.
        /// </summary>
        /// <param name="raftUniqueIndex">The unique index of the Raft item to get the Archipelago locationId for</param>
        /// <returns>The Archipelago locationId, or -1 if not assigned</returns>
        public int getArchipelagoLocationId(int raftUniqueIndex)
        {
            return _raftToArchipelagoIds.GetValueOrDefault(raftUniqueIndex, -1);
        }
    }
}
