using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(ChatManager), "HandleChatMessageInput", typeof(string), typeof(CSteamID))]
	public class HarmonyPatch_ChatManager_HandleChatMessageInput
	{
		[HarmonyPrefix]
		public static bool HandleChatMessageInput_AlwaysReplace(string text, CSteamID textWriterSteamID,
			ChatManager __instance)
		{
			bool flag = __instance.chatFieldController.HandleChatMessageAsCheat(text, textWriterSteamID);
			bool flag2 = __instance.chatFieldController.HandleChatMessageAsTerminalCommando(text, textWriterSteamID);
			if (!flag && !flag2)
			{
				if (textWriterSteamID != null && textWriterSteamID.m_SteamID == __instance.network?.LocalSteamID.m_SteamID)
				{
					ComponentManager<IArchipelagoLink>.Value.SendChatMessage(text);
				}
				else
                {
					__instance.chatFieldController.AddUITextMessage(text, textWriterSteamID);
				}
			}
			typeof(ChatManager).GetMethod("SendRecieveChatMessageAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null);
			return false;
		}
	}
}