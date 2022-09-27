using HarmonyLib;
using Raftipelago.Network;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(PlayerStats), "Damage", typeof(float), typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector3), typeof(EntityType), typeof(SO_Buff))]
	public class HarmonyPatch_PlayerStats_Damage
    {
        [HarmonyPrefix]
        public static bool SometimesReplace(float damage, UnityEngine.Vector3 hitPoint, UnityEngine.Vector3 hitNormal, EntityType damageInflictorEntityType, SO_Buff buffAsset)
        {
            if (damage < 99999)
            {
                Logger.Trace("Doing damage");
                return true;
            }
            else
            {
                Logger.Trace("Damage heuristically identified as DeathLink damage, skipping PlayerStats damage");
                return false;
            }
        }

        [HarmonyPostfix]
        public static void NeverReplace(float damage, UnityEngine.Vector3 hitPoint, UnityEngine.Vector3 hitNormal, EntityType damageInflictorEntityType, SO_Buff buffAsset,
			PlayerStats __instance,
			Network_Player ___playerNetwork)
        {
            Logger.Debug($"Player hurt by damage {damage} | {hitPoint.magnitude} | {hitNormal.magnitude} | {damageInflictorEntityType} | {buffAsset}");
            if (damage < 99999)
            {
                if (___playerNetwork.IsLocalPlayer && !Raft_Network.InMenuScene && __instance.IsDead)
                {
                    Logger.Debug($"Player killed by damage {damage} | {hitPoint.magnitude} | {hitNormal.magnitude} | {damageInflictorEntityType} | {buffAsset}");
                    switch (damageInflictorEntityType)
                    {
                        case EntityType.Player:
                            ComponentManager<MultiplayerComms>.Value.SendDeathLink("Another player");
                            break;
                        case EntityType.Enemy:
                            ComponentManager<MultiplayerComms>.Value.SendDeathLink("Lost too much health");
                            break;
                        case EntityType.FallDamage:
                            ComponentManager<MultiplayerComms>.Value.SendDeathLink("Fell too hard");
                            break;
                        case EntityType.Environment:
                            ComponentManager<MultiplayerComms>.Value.SendDeathLink("The environment");
                            break;
                            // Do NOT specify default, as we don't want to re-send a DeathLink immediately after receiving one
                    }
                }
            }
            else
            {
                Logger.Debug("Damage heuristically identified as DeathLink damage, skipping...");
            }
        }
	}
}