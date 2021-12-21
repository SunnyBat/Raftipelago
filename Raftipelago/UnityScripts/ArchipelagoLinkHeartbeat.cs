using Raftipelago.Network;
using System.Collections;
using UnityEngine;

namespace Raftipelago.UnityScripts
{
    public class ArchipelagoLinkHeartbeat
    {
        public static IEnumerator CreateNewHeartbeat(IArchipelagoLink proxy, float delayInSeconds = 1f)
        {
            for (;;)
            {
                proxy.Heartbeat();
                yield return new WaitForSeconds(delayInSeconds);
            }
        }
    }
}
