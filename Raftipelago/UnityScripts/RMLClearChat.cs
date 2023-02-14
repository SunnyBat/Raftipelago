using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Raftipelago.UnityScripts
{
    public class RMLClearChat
    {
        public static IEnumerator CreateNewRMLClearChat(float delayInSeconds = 10f)
        {
            for (;;)
            {
                // Hack around RML never deleting chat messages and causing larger and larger lag spikes on message print
                // Thanks RML
                var chatContent = (GameObject)typeof(RaftModLoader.RChat).GetField("chatcontent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(RaftModLoader.RChat.instance);
                var chatMessageCount = chatContent?.transform?.childCount ?? 0;
                Logger.Debug($"Current chat length: {chatMessageCount}");
                for (int i = 0; i < chatMessageCount - 50; i++)
                {
                    Logger.Trace($"Removing chat message number {chatMessageCount}");
                    MonoBehaviour.Destroy(chatContent.transform.GetChild(i).gameObject);
                }
                yield return new WaitForSeconds(delayInSeconds);
            }
        }
    }
}
