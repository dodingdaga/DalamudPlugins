using Dalamud.Logging;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PuppetMaster
{
    internal class CommonHelper
    {
        public static bool RegExpMatch(string text, string regexp)
        {
            bool flag = false;

            if (regexp.Trim() == "")
            {
                flag = true;
            } else {
                string pattern = "" + regexp;

                try {
                    if (Regex.Match(text, pattern, RegexOptions.IgnoreCase).Success)
                    {
                        flag = true;
                    }
                } catch (Exception ex)
                {
                    Service.Logger.Error("[PUPPETMASTER] Wrong RegEXP" + regexp);
                }
            }

            return flag;
        }
    }
}
