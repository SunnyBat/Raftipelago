using HarmonyLib;
using Raftipelago.Network;
using Steamworks;
using System.Reflection;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(ChatManager), "HandleChatMessageInput", typeof(string), typeof(CSteamID))]
	public class HarmonyPatch_ChatManager_HandleChatMessageInput
	{
		[HarmonyPrefix]
		public static bool AlwaysReplace(string text, CSteamID textWriterSteamID,
			ChatManager __instance)
		{
			bool flag = __instance.chatFieldController.HandleChatMessageAsCheat(text, textWriterSteamID);
			bool flag2 = __instance.chatFieldController.HandleChatMessageAsTerminalCommando(text, textWriterSteamID);
			if (!flag && !flag2)
			{
				if (Semih_Network.IsHost)
				{
					if (textWriterSteamID.m_SteamID == __instance.network?.LocalSteamID.m_SteamID) // Local player (host) sending a message
					{
						ComponentManager<IArchipelagoLink>.Value.SendChatMessage(text);
					}
					else if (CommonUtils.TryGetArchipelagoPlayerIdFromSteamId(textWriterSteamID.m_SteamID, out int playerId)) // Archipelago sending a message
					{
						Message_IngameChat message = new Message_IngameChat(Messages.Ingame_Chat_Message, __instance, textWriterSteamID, text);
						ComponentManager<Semih_Network>.Value.RPC(message, Target.Other, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
						__instance.chatFieldController.AddUITextMessage(text, textWriterSteamID);
					}
					else if (textWriterSteamID.IsValid()) // Networked player sending a message
					{
						ComponentManager<IArchipelagoLink>.Value.SendChatMessage($"(Local Player {SteamFriends.GetFriendPersonaName(textWriterSteamID)}): {text}");
					}
				}
				else
				{
					__instance.chatFieldController.AddUITextMessage(text, textWriterSteamID); // Always just write it
				}
			}
			typeof(ChatManager).GetMethod("SendRecieveChatMessageAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null);
			return false;
		}
	}
}