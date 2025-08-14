using System;

namespace PuppetMaster
{
    public class BlacklistedPlayer
    {
        public readonly string Id = "";
        public string PlayerName = string.Empty;
        public string PlayerWorld = string.Empty;
        public bool Enabled = true;
        public bool StrictPlayerName = true;

        public BlacklistedPlayer(string name = "")
        {
            PlayerName = name;
            string uniqueId = Guid.NewGuid().ToString();
            Id = uniqueId;
        }
    }
}