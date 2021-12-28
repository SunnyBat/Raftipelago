using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Notification_Research), "Show")]
	public class HarmonyPatch_Notification_Research_ShowNotification
	{
		[HarmonyPostfix]
		public static void Postfix(
			Notification_Research __instance)
		{
			if (__instance != null || __instance == null) // Don't run but keep atm
            {
				return;
            }
			foreach (var itm in __instance.gameObject.GetComponentsInChildren<MonoBehaviour>())
			{
				Debug.Log("===");
				Debug.Log(itm.name);
				Debug.Log(itm.GetType());
				if (itm.name == "You learned" && itm.GetType() == typeof(Text))
                {
					((Text)itm).text = "YL";
                }
			}
			GetFieldValue<Text>(__instance, "text_researchedBy").text = "TRB";
			GetFieldValue<Text>(__instance, "text_learnedItem").text = "TLI";
		}

		private static T GetFieldValue<T>(object obj, string name)
        {
			return (T)typeof(Notification_Research).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
		}
	}
}
