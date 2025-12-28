using PuppetMaster;
using System.Collections.Generic;

public class GlobalSettings
{
    public bool UseGlobalPlayerLists { get; set; } = true;
    public bool UseGlobalCommandLists { get; set; } = true;

    public List<string> GlobalPlayerWhitelist { get; set; } = [];
    public List<string> GlobalPlayerBlacklist { get; set; } = [];

    public List<string> GlobalCommandWhitelist { get; set; } = [];
    public List<string> GlobalCommandBlacklist { get; set; } = [];

    public float GlobalDelaySeconds { get; set; } = 0f;
    public float GlobalCooldownSeconds { get; set; } = 0f;

    public SpeakerFilterMode GlobalSpeakerFilter { get; set; } = SpeakerFilterMode.All;
    public bool UseGlobalChannels { get; set; } = true;
    public List<int> GlobalEnabledChannels { get; set; } = [];

    public List<int> GetEffectiveChannels(Reaction reaction)
    {
        if (!UseGlobalChannels || !reaction.UseGlobalChannels)
            return reaction.EnabledChannels ?? new List<int>();
        return GlobalEnabledChannels ?? new List<int>();
    }
    public bool UseGlobalGameStateRestrictions { get; set; } = true;
    public bool GlobalDisableInCombat { get; set; } = true;
    public bool GlobalDisableInCutscene { get; set; } = true;
    public bool GlobalDisableWhileLoading { get; set; } = true;
}
