﻿using HarmonyLib;
using Raftipelago.Data;
using System;
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
			if (__instance.isFrequencyPoint && ComponentManager<ArchipelagoDataManager>.Value.TryGetSlotData("IslandGenerationDistance", out long distance))
			{
				if (__instance.ChunkPointType == ChunkPointType.Landmark_Utopia)
                {
					// Utopia spawns inside Temperance if loaded when at Temperance and at 1/4 spawn distance. Prevent this from happening.
					// In the future, we should be able to detect if this is going to happen and increase the distance that way (throw this
					// into DoesPointPassSpawnRules())
					distance = (long)Math.Max(distance, 4.0);
                }
				var convertedDistance = (float)(distance / 8.0);
				__result = new Interval_Float(__result.minValue * convertedDistance, __result.maxValue * convertedDistance);
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
			if (__instance.isFrequencyPoint && ComponentManager<ArchipelagoDataManager>.Value.TryGetSlotData("IslandGenerationDistance", out long distance))
			{
				__result = !___useMinDistance || !___others.Contains(pointToCompare.rule.ChunkPointType) || pointToCheck.worldPosition.DistanceXZ(pointToCompare.worldPosition) >= ___minDistanceToOthers * (distance / 8.0);
				return false;
			}
			return true;
		}
	}
}
