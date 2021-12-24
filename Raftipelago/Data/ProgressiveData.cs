using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago.Data
{
    public class ProgressiveData
    {
        /// <summary>
        /// Maps a given progressive technology to its underlying items given. The key is the progressive
        /// technology name (eg "progressive-engine"), and the value is the steps per unlock.<br/>
        /// For example, index 0 is Step 1 and will be unlocked when the first progressive technology is
        /// unlocked. This index might have two items to unlock, and thus will have two strings in the
        /// array at Mappings[progName][0]. Meanwhile, index 1 is Step 2 and will be unlocked when the
        /// second progressive technology is unlocked. This index might have one item to unlock, and thus
        /// will have one string in the array at Mappings[progName][1].
        /// </summary>
        public readonly ReadOnlyDictionary<string, string[][]> Mappings;

        public ProgressiveData(EmbeddedFileUtils utils)
        {
            var rawMappings = utils.ReadTextFile("Data", "ProgressiveData.json");
            UnityEngine.Debug.Log(rawMappings);
            var parsedMappings = JsonConvert.DeserializeObject<Dictionary<string, string[][]>>(rawMappings);
            Mappings = new ReadOnlyDictionary<string, string[][]>(parsedMappings);
        }
    }
}
