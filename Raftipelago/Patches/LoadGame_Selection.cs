using HarmonyLib;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(LoadGame_Selection), "SetInfo", typeof(GameToFolderConnection))]
	public class HarmonyPatch_LoadGame_Selection_SetInfo
	{
		[HarmonyPostfix]
		public static void Postfix(GameToFolderConnection con,
			LoadGame_Selection __instance)
		{
			if (!CommonUtils.IsValidRaftipelagoSave(con.rgdGame)) // Will always return true if vanilla would set image_error to active
			{
				__instance.image_error.gameObject.SetActiveSafe(true);
			}
		}
	}

	[HarmonyPatch(typeof(LoadGame_Selection), "get_IsSelectionLoadable")]
	public class HarmonyPatch_LoadGame_Selection_get_IsSelectionLoadable
	{
		[HarmonyPostfix]
		public static void Postfix(
			ref bool __result,
			LoadGame_Selection __instance)
		{
			__result &= CommonUtils.IsValidRaftipelagoSave(__instance.rgdGame);
		}
	}
}