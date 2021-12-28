using HarmonyLib;
using Raftipelago.Data;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(NotificationManager), "ShowNotification", typeof(string))]
	public class HarmonyPatch_NotificationManager_ShowNotification
	{
		[HarmonyPrefix]
		public static void SometimesReplace(
			string identifier,
			ref List<Notification> ___notifications
			)
		{
			if (identifier != "OnlyProcessThisIdentifier")
            {
				return;
            }

			Debug.Log($"Notifications valid = {___notifications != null}");
			Debug.Log($"nC: {___notifications.Count} {___notifications.Where(not => string.IsNullOrWhiteSpace(not.identifier)).Count()}");
			var notificationOld = ___notifications.Find(n => n.identifier == identifier); // This is false
			var index = ___notifications.FindIndex(n => n.identifier == identifier); // But this is 4 -- WTF???
			Debug.Log($"NotificationOld = {notificationOld != null}");
			Debug.Log($"NI = {index}");
			Debug.Log($"NAI = {___notifications[index] != null}");
			if (index >= 0)
            {
                ___notifications.RemoveAt(index);
            }
            ___notifications.Add(new Notification_ArchipelagoSent());
            if (identifier == "ArchipelagoSent")
			{
				var notification = ___notifications.Find(n => n.identifier == identifier);
				Debug.Log($"ASNotificationValid = {notification != null}");
			}
        }
    }

    public class Notification_ArchipelagoSent : Notification_Animated
	{
		public Notification_ArchipelagoSent()
		{
			this.identifier = "ArchipelagoSent";
		}

		public override void Show()
		{
			Debug.Log("Show1");
			Notification_ArchipelagoSent_Info notificationToShow = this.archipelagoSentQueue.Dequeue();
			this.text_foundBy.text = notificationToShow.researcher;
			this.text_sentTo.text = notificationToShow.sentToPlayerName;
			this.text_locationName.text = notificationToShow.foundItemName;
			this.image_archipelago.sprite = ComponentManager<SpriteManager>.Value.GetArchipelagoSprite();
			base.Show();
		}

		public Queue<Notification_ArchipelagoSent_Info> archipelagoSentQueue = new Queue<Notification_ArchipelagoSent_Info>();

		[SerializeField]
		private Text text_locationName;

		[SerializeField]
		private Text text_foundBy;

		[SerializeField]
		private Text text_sentTo;

		[SerializeField]
		private Image image_archipelago;
	}

	public class Notification_ArchipelagoSent_Info
	{
		public string foundItemName;
		public string researcher;
		public string sentToPlayerName;

		public Notification_ArchipelagoSent_Info(string foundItemName, string researcher, string sentToPlayerName)
		{
			this.foundItemName = foundItemName;
			this.researcher = researcher;
			this.sentToPlayerName = sentToPlayerName;
		}
	}
}
