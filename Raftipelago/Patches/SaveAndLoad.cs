using HarmonyLib;
using Raftipelago.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace Raftipelago.Patches
{
	[HarmonyPatch(typeof(SaveAndLoad), "RestoreRGDGame", typeof(RGD_Game))]
	public class HarmonyPatch_SaveAndLoad_RestoreRGDGame
	{
		[HarmonyPostfix]
		public static void Postfix(RGD_Game game)
		{
			Debug.Log("RRGDG1");
			if (game.GetType() == typeof(RGD_Game_Raftipelago))
			{
				Debug.Log("RRGDG2");
				var castedGame = (RGD_Game_Raftipelago)game;
				ComponentManager<IArchipelagoLink>.Value.SetUnlockedResourcePacks(castedGame.Raftipelago_ItemPacks);
				Debug.Log("RRGDG3");
			}
			else
			{
				Debug.Log("RRGD4");
				ComponentManager<IArchipelagoLink>.Value.SetUnlockedResourcePacks(new List<int>());
			}
			UnityEngine.Debug.LogError("game is not Raftipelago game save data.");
        }
    }

	[HarmonyPatch(typeof(SaveAndLoad), "CreateRGDGame")]
	public class HarmonyPatch_SaveAndLoad_CreateRGDGame
	{
		[HarmonyPostfix]
		public static void Postfix(
			ref RGD_Game __result)
		{
			var res = new RGD_Game_Raftipelago(__result);
			res.Raftipelago_ItemPacks = ComponentManager<IArchipelagoLink>.Value.GetAllUnlockedResourcePacks();
			__result = res;
		}
	}

	[HarmonyPatch(typeof(SaveAndLoad), "GetRGDGamesFromFolder", typeof(string))]
    public class HarmonyPatch_SaveAndLoad_GetRGDGamesFromFolder
    {
        [HarmonyPrefix]
        public static bool Postfix(string folderPath,
            ref RGD_Game[] __result,
            SaveAndLoad __instance)
        {
            List<RGD_Game> list = new List<RGD_Game>();
            foreach (FileInfo fileInfo in __instance.GetAllRGDFileInfoFromFolder(folderPath))
            {
                if (fileInfo != null)
                {
                    // The only reason we're overriding this method is to change this type to the Raftipelago type.
                    // This is because constructor return values cannot be overridden, so we need to make the Load
                    // method call our object's constructor instead of the default one.
                    // This can be extended to something like the Extra Settings API. Right now, that uses a JSON
                    // file to store per-world settings. That's the safer way. This, however, is (IMO) more elegant,
                    // and can serve as a demonstration of how this can work and be extended.
                    // We also need to be able to easily disable worlds in the Load Game menu, and associating data
                    // like this is just a touch easier down the line.
                    try
                    {
                        RGD_Game rgd_Game = tst2<RGD_Game>(folderPath + fileInfo.Name);
                        if (rgd_Game != null)
                        {
                            list.Add(rgd_Game);
                        }
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.Log(e);
                    }
                }
            }
            list = (from g in list
                    orderby g.lastPlayedDateTicks
                    select g).ToList<RGD_Game>();
            list.Reverse();
            __result = list.ToArray();
            return false;
        }

		private static T tst2<T>(string filename)
		{
			if (!filename.EndsWith(".rgd"))
			{
				filename += ".rgd";
			}
			if (File.Exists(filename))
			{
				BinaryFormatter binaryFormatter = new BinaryFormatter();
				FileStream fileStream = FileManager.OpenFile(filename, FileMode.Open);
				bool flag = false;
				T result = default(T);
				if (fileStream != null)
				{
					try
					{
						var res = binaryFormatter.Deserialize(fileStream);
						Debug.Log(res);
						Debug.Log(res.GetType());
						result = (T)res;
					}
					catch (Exception message)
					{
						flag = true;
						Debug.Log(message);
						fileStream.Close();
					}
					fileStream.Close();
				}
				else
				{
					flag = true;
				}
				if (!flag)
				{
					return result;
				}
			}
			return default(T);
		}
    }

	[HarmonyPatch(typeof(SaveAndLoad), "GetLatestRGDGameFromBackupFolders")]
	public class tst
	{
		[HarmonyPrefix]
		public static bool Prefix(DirectoryInfo worldDirInfo, ref DirectoryInfo rgdGameDirInfo,
			ref RGD_Game __result,
			SaveAndLoad __instance)
		{
			Debug.Log("Loading " + worldDirInfo.Name);
			rgdGameDirInfo = null;
			DirectoryInfo[] directories = worldDirInfo.GetDirectories();
			List<DirectoryInfo> list = new List<DirectoryInfo>();
			foreach (DirectoryInfo directoryInfo in directories)
			{
				if (directoryInfo.FullName.EndsWith("-Latest"))
				{
					list.Add(directoryInfo);
				}
			}
			DateTime a = DateTime.MinValue;
			string text = string.Empty;
			foreach (DirectoryInfo directoryInfo2 in list)
			{
				if (directoryInfo2.GetFiles().Length != 0)
				{
					DateTime dateTime;
					SaveAndLoad.ConvertBackupFolderNameToDateTime(directoryInfo2.Name, out dateTime);
					if (a.CompareTo(dateTime) < 0)
					{
						a = dateTime;
						text = directoryInfo2.FullName;
					}
				}
			}
			bool flag = false;
			if (!(text != string.Empty))
			{
				if (!flag)
				{
					string text2 = string.Empty;
					DateTime value = DateTime.MinValue;
					foreach (DirectoryInfo directoryInfo3 in directories)
					{
						DateTime dateTime2;
						SaveAndLoad.ConvertBackupFolderNameToDateTime(directoryInfo3.Name, out dateTime2);
						if (dateTime2.CompareTo(value) > 0)
						{
							value = dateTime2;
							text2 = directoryInfo3.FullName;
						}
					}
					if (text2 != string.Empty)
					{
						DirectoryInfo directoryInfo4 = new DirectoryInfo(text2);
						rgdGameDirInfo = directoryInfo4;
						RGD_Game[] rgdgamesFromFolder = __instance.GetRGDGamesFromFolder(directoryInfo4.FullName + "\\");
						if (rgdgamesFromFolder.Length != 0)
						{
							__result = rgdgamesFromFolder[0];
							return false;
						}
					}
				}
				__result = null;
				return false;
			}
			rgdGameDirInfo = new DirectoryInfo(text);
			RGD_Game[] rgdgamesFromFolder2 = __instance.GetRGDGamesFromFolder(text + "\\");
			if (rgdgamesFromFolder2.Length != 0)
			{
				__result = rgdgamesFromFolder2[0];
				return false;
			}
			__result = null;
			return false;
		}
	}

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
				Debug.Log("RIP = " + Raftipelago_ItemPacks);
			}
			catch (Exception e)
			{
				Debug.Log(e.Message);
			} // SavedData will default to null, signaling that this is not a Raftipelago world (we could use a flag instead)
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
