using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace Raftipelago.Patches
{
    [HarmonyPatch(typeof(ResearchTable), "Open", typeof(bool), typeof(CSteamID))]
    public class HarmonyPatch_ResearchTable
    {
        [HarmonyPostfix]
        public static void Open(bool __result, ResearchTable __instance)
        {
            Debug.Log("Research table opened! 3");
        }
    }
}
