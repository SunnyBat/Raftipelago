using HarmonyLib;
using System.Collections.Generic;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(SO_ChunkSpawnRuleAsset), "get_SpawnDistanceFromRaft")]
	public class HarmonyPatch_SO_ChunkSpawnRuleAsset_get_SpawnDistanceFromRaft
	{
		[HarmonyPostfix]
		public static void SometimesModifyResult(
			SO_ChunkSpawnRuleAsset __instance,
			ref Interval_Float __result)
		{
			if (__instance.isFrequencyPoint)
			{
				// TODO Scale minValue and maxValue by some value
				__result = new Interval_Float(__result.minValue, __result.maxValue);
			}
		}
	}
	[HarmonyPatch(typeof(SO_ChunkSpawnRuleAsset), "DoesPointPassSpawnRules", typeof(ChunkPoint), typeof(ChunkPoint))]
	public class HarmonyPatch_SO_ChunkSpawnRuleAsset_DoesPointPassSpawnRules
	{
		[HarmonyPrefix]
		public static bool SometimesModify(ChunkPoint pointToCheck, ChunkPoint pointToCompare,
			SO_ChunkSpawnRuleAsset __instance,
			ref bool __result,
			bool ___useMinDistance,
			float ___minDistanceToOthers,
			List<ChunkPointType> ___others)
		{
			if (__instance.isFrequencyPoint)
			{
				// TODO Scale ___minDistanceToOthers by some value
				__result = !___useMinDistance || !___others.Contains(pointToCompare.rule.ChunkPointType) || pointToCheck.worldPosition.DistanceXZ(pointToCompare.worldPosition) >= ___minDistanceToOthers;
				return false;
			}
			return true;
		}
	}
}
