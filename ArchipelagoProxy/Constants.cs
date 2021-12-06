using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArchipelagoProxy
{
    public class Constants
    {

        // We are assuming that these strings will never appear in message bodies -- reasonable for JSON we're sending, but
        // if we end up sending raw data (eg images or something) then we need to find a new way of doing this, since the
        // data can theoretically contain any of these indicators
        public const string StopConnectionMessageType = "Raftipelago-StopProxy";
        public const string MessageTypeStartStr = "<<mT>>";
        public const string MessageTypeEndStr = "<</mT>>";
        public const string MessageEndStr = "<<EOM/>>";
        public static readonly byte[] MessageTypeStartBytes = Encoding.UTF8.GetBytes(MessageTypeStartStr);
        public static readonly byte[] MessageTypeEndBytes = Encoding.UTF8.GetBytes(MessageTypeEndStr);
        public static readonly byte[] MessageEndBytes = Encoding.UTF8.GetBytes(MessageEndStr);
    }
}
