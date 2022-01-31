using HarmonyLib;
using System.Runtime.Serialization;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(RGD_Game), "GetObjectData", typeof(SerializationInfo), typeof(StreamingContext))]
	public class HarmonyPatch_RGD_Game_GetObjectData
	{
		[HarmonyPostfix]
		public static void Postfix(SerializationInfo info, StreamingContext sc)
		{
			info.AddValue("Raftipelago-ReceivedItems", ComponentManager<ItemTracker>.Value.GetAllReceivedItemIds());
		}
	}
}
