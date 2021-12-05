using HarmonyLib;
using Raftipelago.Data;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Raftipelago.Patches
{
    [HarmonyPatch(typeof(NotificationManager), "ShowNotification", typeof(string))]
    public class HarmonyPatch_NotificationManager_ShowNotification
    {
        [HarmonyPostfix]
        public static void ShowNotification_OptionalReplace(
            string identifier,
            ref List<Notification> ___notifications
            )
        {
            Notification notification = ___notifications.Find((Notification n) => n.identifier == identifier);
			if (notification == null && identifier == "ArchipelagoSent")
            {
				___notifications.Add(new Notification_ArchipelagoSent());
            }
        }
    }

    public class Notification_ArchipelagoSent : Notification_Animated
	{
		public override void Show()
		{
			Notification_ArchipelagoSent_Info notification_Research_Info = this.archipelagoSentQueue.Dequeue();
			LocalizationParameters.remoteSteamID = notification_Research_Info.researcher;
			this.text_foundBy.text = "Found by"; // TODO Localize?
			this.text_sentTo.text = notification_Research_Info.sentToPlayerName;
			this.text_locationName.text = notification_Research_Info.foundItemName;
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

		// Token: 0x04001159 RID: 4441
		[SerializeField]
		private Image image_archipelago;
	}

	public class Notification_ArchipelagoSent_Info
	{
		public string foundItemName;
		public CSteamID researcher;
		public string sentToPlayerName;

		public Notification_ArchipelagoSent_Info(string foundItemName, CSteamID researcher, string sentToPlayerName)
		{
			this.foundItemName = foundItemName;
			this.researcher = researcher;
			this.sentToPlayerName = sentToPlayerName;
		}
	}
}
