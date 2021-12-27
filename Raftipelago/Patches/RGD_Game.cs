using HarmonyLib;
using Raftipelago.Network;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(RGD_Game), "GetObjectData", typeof(SerializationInfo), typeof(StreamingContext))]
	public class HarmonyPatch_RGD_Game_GetObjectData
	{
		[HarmonyPostfix]
		public static void Postfix(SerializationInfo info, StreamingContext sc)
		{
			info.AddValue("Raftipelago-ItemPacks", ComponentManager<IArchipelagoLink>.Value.GetAllUnlockedResourcePacks());
		}
	}
}
