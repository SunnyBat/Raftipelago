using HarmonyLib;
using Raftipelago.Data;
using Raftipelago.Network;
using UnityEngine;

namespace Raftipelago.Patches
{

	[HarmonyPatch(typeof(CharacterUnlock), "Interact", typeof(Network_Player))]
	public class HarmonyPatch_CharacterUnlock_Interact
	{
		[HarmonyPrefix]
		public static void NeverReplace(Network_Player interactor,
			SO_Character ___characterToUnlock,
			GameObject ___characterModel)
		{
			if (___characterToUnlock != null && ___characterModel != null && ___characterModel.activeSelf)
			{
				var characterName = CommonUtils.TryGetOrKey(ComponentManager<ExternalData>.Value.UniqueLocationNameToFriendlyNameMappings, ___characterToUnlock.name);
				(ComponentManager<NotificationManager>.Value.ShowNotification("Research") as Notification_Research).researchInfoQue.Enqueue(new Notification_Research_Info(characterName, interactor.steamID, ComponentManager<SpriteManager>.Value.GetArchipelagoSprite()));
				if (Raft_Network.IsHost)
				{
					var friendlyName = characterName;
					ComponentManager<IArchipelagoLink>.Value.LocationUnlocked(friendlyName);
				}
			}
		}
	}
}
