using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class RaftipelagoTwo : Mod
{
    private Harmony patcher;
    public void Start()
    {
        patcher = new Harmony("com.github.sunnybat.raftipelago");
        patcher.PatchAll(Assembly.GetExecutingAssembly());
        Debug.Log("Mod Raftipelago has been loaded!");
    }

    public void OnModUnload()
    {
        patcher.UnpatchAll("com.github.sunnybat.raftipelago");
        Debug.Log("Mod Raftipelago has been unloaded!");
    }
}