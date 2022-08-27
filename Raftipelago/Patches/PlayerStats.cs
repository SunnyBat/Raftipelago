using HarmonyLib;
using Raftipelago.Network;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(PlayerStats), "Damage", typeof(float), typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector3), typeof(EntityType), typeof(SO_Buff))]
	public class HarmonyPatch_PlayerStats_Damage
	{
		[HarmonyPostfix]
		public static void NeverReplace(float damage, UnityEngine.Vector3 hitPoint, UnityEngine.Vector3 hitNormal, EntityType damageInflictorEntityType, SO_Buff buffAsset,
			PlayerStats __instance)
		{
			if (!Raft_Network.InMenuScene && Raft_Network.IsHost && __instance.IsDead)
			{
				Logger.Debug("Player killed by damage");
				switch (damageInflictorEntityType)
				{
					case EntityType.Player:
						ComponentManager<IArchipelagoLink>.Value.PlayerDied("Another player");
						break;
					case EntityType.Enemy:
						ComponentManager<IArchipelagoLink>.Value.PlayerDied("Lost too much health");
						break;
					case EntityType.FallDamage:
						ComponentManager<IArchipelagoLink>.Value.PlayerDied("Fell too hard");
						break;
					case EntityType.Environment:
						ComponentManager<IArchipelagoLink>.Value.PlayerDied("The environment");
						break;
						// Do NOT specify default, as we don't want to re-send a DeathLink immediately after receiving one
				}
			}
		}
	}
}