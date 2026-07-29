using System;
using System.Text.RegularExpressions;

namespace PuppetMaster;

internal enum ReactionMatchStatus
{
    NoMatch,
    Success,
    TimedOut,
    InvalidReplacement,
}

internal static class ReactionCommandMatcher
{
    public static Regex? SelectPattern(Reaction reaction)
    {
        return reaction.UseRegex ? reaction.CustomRx : reaction.Rx;
    }

    public static ReactionMatchStatus TryGenerateCommand(
        Regex? pattern,
        string message,
        string replacement,
        out string command,
        out string? error)
    {
        command = string.Empty;
        error = null;
        if (pattern == null)
            return ReactionMatchStatus.NoMatch;
        try
        {
            var match = pattern.Match(message);
            if (!match.Success)
                return ReactionMatchStatus.NoMatch;
            command = match.Result(replacement);
            return ReactionMatchStatus.Success;
        }
        catch (RegexMatchTimeoutException)
        {
            return ReactionMatchStatus.TimedOut;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return ReactionMatchStatus.InvalidReplacement;
        }
    }
}
