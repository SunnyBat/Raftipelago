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
            // TODO Pull from Archipelago server eventually
            ItemMap[] values = JsonConvert.DeserializeObject<ItemMap[]>(EmbeddedFileUtils.ReadFile("Data", "ItemMapping.json"));
            foreach (var mapping in values)
            {
                _archipelagoToRaftIds.Add(mapping.archipelagoLocationId, mapping.raftUniqueIndex);
                _raftToArchipelagoIds.Add(mapping.raftUniqueIndex, mapping.archipelagoLocationId);
            }
        }

        public int getRaftUniqueIndex(int archipelagoLocationId)
        {
            return _archipelagoToRaftIds.GetValueOrDefault(archipelagoLocationId, -1);
        }

        public int getArchipelagoLocationId(int raftUniqueIndex)
        {
            return _raftToArchipelagoIds.GetValueOrDefault(raftUniqueIndex, -1);
        }
    }
}
