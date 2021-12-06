using ArchipelagoProxy.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ArchipelagoProxy
{
    public class RaftPacketConverter
    {
        public static Dictionary<string, Func<JObject, dynamic>> PacketDeserializationMap = new Dictionary<string, Func<JObject, dynamic>> ()
        {
            ["PacketType1"] = (JObject obj) => obj.ToObject<dynamic>(),
            ["PacketType2"] = (JObject obj) => obj.ToObject<dynamic>()
        };

        public BasePacket parsePacket(string json)
        {
            return null;
        }
    }
}
