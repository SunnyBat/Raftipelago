using HarmonyLib;
using Raftipelago.Network;
using Steamworks;
using System.Reflection;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(ChatManager), "HandleChatMessageInput", typeof(string), typeof(Network_UserId))]
	public class HarmonyPatch_ChatManager_HandleChatMessageInput
	{
		[HarmonyPrefix]
		public static bool AlwaysReplace(string text, Network_UserId textWriterSteamID,
			Raft_Network ___network,
			ChatManager __instance)
		{
			bool flag = __instance.chatFieldController.HandleChatMessageAsCheat(text, textWriterSteamID);
			bool flag2 = __instance.chatFieldController.HandleChatMessageAsTerminalCommando(text, textWriterSteamID);
			if (!flag && !flag2)
			{
				if (Raft_Network.IsHost)
				{
					if (CommonUtils.TryGetArchipelagoPlayerIdFromSteamId(textWriterSteamID.Id, out int playerId)) // Archipelago sending a message
					{
						Message_IngameChat message = new Message_IngameChat(Messages.Ingame_Chat_Message, __instance, textWriterSteamID, text);
						ComponentManager<Raft_Network>.Value.RPC(message, Target.Other, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
						if (_shouldDisplayArchipelagoMessage(textWriterSteamID))
						{
							__instance.chatFieldController.AddUITextMessage(text, textWriterSteamID);
						}
					}
					else if (textWriterSteamID.IsValid()) // Networked player sending a message
					{
						ComponentManager<IArchipelagoLink>.Value.SendChatMessage($"(Raft Player {___network.GetPlayerFromID(textWriterSteamID)?.visualName}): {text}");
					}
				}
				else if (CommonUtils.TryGetArchipelagoPlayerIdFromSteamId(textWriterSteamID.Id, out int playerId)) // Only send Archipelago messages in chat
				{
					if (_shouldDisplayArchipelagoMessage(textWriterSteamID))
					{
						__instance.chatFieldController.AddUITextMessage(text, textWriterSteamID);
					}
				}
			}
			typeof(ChatManager).GetMethod("SendRecieveChatMessageAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, null);
			return false;
		}

		private static bool _shouldDisplayArchipelagoMessage(Network_UserId archipelagoSteamID)
		{
			var mod = ComponentManager<RaftipelagoMod>.Value;
			var mode = mod == null ? RaftipelagoMod.MessageFilterMode.All : mod.GetMessageFilterMode();
			switch (mode)
			{
				case RaftipelagoMod.MessageFilterMode.None:
					return false;
				case RaftipelagoMod.MessageFilterMode.MineOnly:
					return CommonUtils.IsMessageSlotRelated(archipelagoSteamID.Id);
				default:
					return true;
			}
		}
	}

	[HarmonyPatch(typeof(ChatManager), "SendChatMessage", typeof(string), typeof(Network_UserId))]
	public class HarmonyPatch_ChatManager_SendChatMessage
	{
		[HarmonyPrefix]
		public static bool AlwaysReplace(string p_message, Network_UserId p_steamID,
			Raft_Network ___network,
			ChatManager __instance)
		{
			if (___network == null)
			{
				return false;
			}

			Message_IngameChat message = new Message_IngameChat(Messages.Ingame_Chat_Message, __instance, p_steamID, p_message);
			if (Raft_Network.IsHost)
			{
				ComponentManager<IArchipelagoLink>.Value.SendChatMessage(p_message);
			}
			else
			{
				___network.SendP2P(___network.HostID, message, EP2PSend.k_EP2PSendReliable, NetworkChannel.Channel_Game);
			}
			return false;
		}
	}
}