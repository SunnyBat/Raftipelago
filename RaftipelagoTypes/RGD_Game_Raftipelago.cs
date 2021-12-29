using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace RaftipelagoTypes
{
	[Serializable]
	public class RGD_Game_Raftipelago : RGD_Game
	{
		public const string RaftipelagoItemsFieldName = "Raftipelago-ItemPacks"; // TODO Rename before public release (required for existing saves to work properly)

		public List<int> Raftipelago_ReceivedItems;

		public RGD_Game_Raftipelago()
		{
		}

		public RGD_Game_Raftipelago(RGD_Game baseObj)
		{
			// Dynamically assign existing fields; we're re-creating a new object with the same
			// values/references, and we want this to modify public and non-public fields. We
			// also want this to be able to immediately adapt to updates, which reflection will
			// make happen.
			var myType = typeof(RGD_Game_Raftipelago);
			foreach (var field in baseObj.GetType().GetFields())
			{
				var baseObjValue = field.GetValue(baseObj);
				myType.GetField(field.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(this, baseObjValue);
			}
		}

		public RGD_Game_Raftipelago(SerializationInfo info, StreamingContext sc) : base(info, sc)
		{
			try
			{
				Raftipelago_ReceivedItems = (List<int>)(info.GetValue(RaftipelagoItemsFieldName, typeof(List<int>)) ?? new List<int>());
			}
			catch (Exception) { } // Raftipelago_ReceivedItems will default to null, signaling that this is not a Raftipelago world (we could use a flag instead)
		}

        [OnDeserializing]
        protected override void SetDefaults(StreamingContext sc)
        {
            base.SetDefaults(sc);
            Raftipelago_ReceivedItems = null;
        }
	}
}
