﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace RaftipelagoTypes
{
	[Serializable]
	public class RGD_Game_Raftipelago : RGD_Game
	{
		public RGD_Game_Raftipelago(RGD_Game baseObj)
		{
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
				Raftipelago_ItemPacks = (List<int>)(info.GetValue("Raftipelago-ItemPacks", typeof(List<int>)) ?? new List<int>());
			}
			catch (Exception) { } // SavedData will default to null, signaling that this is not a Raftipelago world (we could use a flag instead)
		}

		[OnDeserializing]
		protected override void SetDefaults(StreamingContext sc)
		{
			base.SetDefaults(sc);
			Raftipelago_ItemPacks = null;
		}

		public List<int> Raftipelago_ItemPacks;
	}
}