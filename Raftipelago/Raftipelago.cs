using UnityEngine;

public class Raftipelago : Mod
{
    public void Start()
    {
        Debug.Log("Mod Raftipelago has been loaded!");
    }

    public void OnModUnload()
    {
        Debug.Log("Mod Raftipelago has been unloaded!");
    }
}