using NoireLib;
using System;
using System.Text.RegularExpressions;

namespace PuppetMaster_Enhanced;

internal class CommonHelper
{
    public static bool RegExpMatch(string text, string regexp)
    {
        bool flag = false;

        if (regexp.Trim() == "")
        {
            flag = true;
        }
        else
        {
            try
            {
                if (Regex.Match(text, regexp, RegexOptions.IgnoreCase).Success)
                {
                    flag = true;
                }
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(ex, $"[PUPPETMASTER] Wrong RegEXP: {regexp}");
            }
        }

        return flag;
    }
}
