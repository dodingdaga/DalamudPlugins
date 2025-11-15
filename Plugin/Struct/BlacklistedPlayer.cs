using System;
using NoireLib.Helpers;

namespace PuppetMaster_Enhanced;

[Serializable]
public class BlacklistedPlayer
{
    public readonly string Id;
    public string PlayerName = string.Empty;
    public string PlayerWorld = string.Empty;
    public bool Enabled = true;
    public bool StrictPlayerName = true;

    public BlacklistedPlayer(string name = "")
    {
        PlayerName = name;
        Id = RandomGenerator.GenerateGuidString();
    }
}