using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago
{
    // We could make extensions, but I don't want to have this in IntelliSense in case it implies
    // that the function is in Raft (thus making it more reliable when updates happen) instead of
    // in our code. Because of this, extensions should not be used for Raft classes.
    public class CommonUtils
    {
        public static bool IsNoteOrBlueprint(LandmarkItem item)
        {
            var name = item?.name ?? "";
            return name.Contains("NoteBookPickup") || name.Contains("Blueprint");
        }
    }
}
