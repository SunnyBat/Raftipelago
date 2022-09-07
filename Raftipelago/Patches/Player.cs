using HarmonyLib;
using Raftipelago.Network;
using System.Diagnostics;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(Player), "Kill", typeof(bool))]
	public class HarmonyPatch_Player_Kill
	{
		[HarmonyPrefix]
		public static void NeverReplace(bool replicating,
			Player __instance,
			PlayerStats ___playerStats)
		{
			Logger.Trace("Kill called (Player)");
			// Kill() sets IsDead to true, if it's already true, it skips everything (since we're already dead)
			// We specifically single out PlayerStats::void Update() to determine if we died due to oxygen/hunger/thirst.
			// If it was, we know to trigger these death types. Otherwise, the death came from some other source of
			// damage, and we'll trigger that instead using PlayerStats.Damage().
			if (!Raft_Network.InMenuScene && !__instance.IsDead && _isDeathDueToPlayerStatsUpdate())
			{
				Logger.Trace("PlayerStats death");
				if (___playerStats.stat_oxygen.IsZero)
				{
					ComponentManager<MultiplayerComms>.Value.SendDeathLink("Suffocated to death");
				}
				else if (___playerStats.stat_thirst.Normal.IsZero)
				{
					ComponentManager<MultiplayerComms>.Value.SendDeathLink("Died of thirst");
				}
				else if (___playerStats.stat_hunger.Normal.IsZero)
				{
					ComponentManager<MultiplayerComms>.Value.SendDeathLink("Starved to death");
				}
				else
				{
					Logger.Warn("Unknown PlayerStats.Update() death");
					ComponentManager<MultiplayerComms>.Value.SendDeathLink("Unknown causes");
				}
			}
		}

		private static bool _isDeathDueToPlayerStatsUpdate()
        {
			StackTrace stackTrace = new StackTrace();
			StackFrame[] stackFrames = stackTrace.GetFrames();
			foreach (var frame in stackFrames)
            {
				// TODO Better detection than this, namely don't ToString() the method...
				if (frame.GetMethod().ReflectedType.FullName == "PlayerStats" && frame.GetMethod().ToString() == "Void Update()")
                {
					Logger.Trace("Death from PlayerStats update");
					return true;
                }
			}
			Logger.Trace("Death NOT from PlayerStats update");
			return false;

		}
	}
}