using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago.Data
{
    public class ExternalData
    {
        public ReadOnlyDictionary<string, string> UniqueItemNameToFriendlyNameMappings { get; private set; }
        public ReadOnlyDictionary<string, string> UniqueLocationNameToFriendlyNameMappings { get; private set; }
        public ReadOnlyDictionary<string, string> FriendlyItemNameToUniqueNameMappings { get; private set; }
        public ReadOnlyDictionary<string, string> FriendlyLocationNameToUniqueNameMappings { get; private set; }
        public ReadOnlyDictionary<string, string> UniqueRegionNameToFriendlyNameMappings { get; private set; }
        public ReadOnlyDictionary<string, string[]> AdditionalLocationCheckItemRequirements { get; private set; }
        /// <summary>
        /// Maps a given progressive technology to its underlying items given. The key is the progressive
        /// technology name (eg "progressive-engine"), and the value is the steps per unlock.<br/>
        /// For example, index 0 is Step 1 and will be unlocked when the first progressive technology is
        /// unlocked. This index might have two items to unlock, and thus will have two strings in the
        /// array at Mappings[progName][0]. Meanwhile, index 1 is Step 2 and will be unlocked when the
        /// second progressive technology is unlocked. This index might have one item to unlock, and thus
        /// will have one string in the array at Mappings[progName][1].
        /// </summary>
        public ReadOnlyDictionary<string, string[][]> ProgressiveTechnologyMappings { get; private set; }
        public ReadOnlyDictionary<string, string[]> QuestLocations { get; private set; }

        public ExternalData(EmbeddedFileUtils utils)
        {
            _loadItems(utils);
            _loadLocations(utils);
            _loadRegions(utils);
            _loadAdditionalLocationRequirements(utils);
            _loadProgressiveData(utils);
            _loadQuestLocations(utils);
        }

        private void _loadItems(EmbeddedFileUtils utils)
        {
            var rawMappings = utils.ReadTextFile("Data", "FriendlyItems.json");
            var parsedMappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawMappings);
            UniqueItemNameToFriendlyNameMappings = new ReadOnlyDictionary<string, string>(parsedMappings);
            FriendlyItemNameToUniqueNameMappings = new ReadOnlyDictionary<string, string>(_inverseDictionary(parsedMappings));
        }

        private void _loadLocations(EmbeddedFileUtils utils)
        {
            var rawMappings = utils.ReadTextFile("Data", "FriendlyLocations.json");
            var parsedMappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawMappings);
            UniqueLocationNameToFriendlyNameMappings = new ReadOnlyDictionary<string, string>(parsedMappings);
            FriendlyLocationNameToUniqueNameMappings = new ReadOnlyDictionary<string, string>(_inverseDictionary(parsedMappings));
        }

        private void _loadRegions(EmbeddedFileUtils utils)
        {
            var rawMappings = utils.ReadTextFile("Data", "FriendlyRegions.json");
            var parsedMappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawMappings);
            UniqueRegionNameToFriendlyNameMappings = new ReadOnlyDictionary<string, string>(parsedMappings);
        }

        private void _loadAdditionalLocationRequirements(EmbeddedFileUtils utils)
        {
            var rawMappings = utils.ReadTextFile("Data", "SpecificLocationRequirements.json");
            var parsedMappings = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(rawMappings);
            AdditionalLocationCheckItemRequirements = new ReadOnlyDictionary<string, string[]>(parsedMappings);
        }

        private void _loadProgressiveData(EmbeddedFileUtils utils)
        {
            var rawMappings = utils.ReadTextFile("Data", "ProgressiveData.json");
            var parsedMappings = JsonConvert.DeserializeObject<Dictionary<string, string[][]>>(rawMappings);
            ProgressiveTechnologyMappings = new ReadOnlyDictionary<string, string[][]>(parsedMappings);
        }

        private void _loadQuestLocations(EmbeddedFileUtils utils)
        {
            var rawMappings = utils.ReadTextFile("Data", "QuestLocations.json");
            var parsedMappings = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(rawMappings);
            QuestLocations = new ReadOnlyDictionary<string, string[]>(parsedMappings);
        }

        private Dictionary<T, T> _inverseDictionary<T>(Dictionary<T, T> dict)
        {
            var inversedMappings = new Dictionary<T, T>();
            foreach (var kvp in dict)
            {
                inversedMappings[kvp.Value] = kvp.Key;
            }
            return inversedMappings;
        }
    }
}
