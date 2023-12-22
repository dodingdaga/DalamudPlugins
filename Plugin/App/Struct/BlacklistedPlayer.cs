using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PuppetMaster
{
    public class BlacklistedPlayer
    {
        public readonly string Id = "";
        public string PlayerName = string.Empty;
        public bool Enabled = true;

        public BlacklistedPlayer(string name = "")
        {
            PlayerName = name;
            string uniqueId = Guid.NewGuid().ToString();
            Id = uniqueId;
        }
    }
}
