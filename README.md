# Doding Daga Plugins

Add this URL to Dalamud's custom plugin repositories:

```text
https://raw.githubusercontent.com/dodingdaga/DalamudPlugins/main/PuppetMaster.json
```

## Puppet Master

Puppet Master watches selected chat channels for phrases and turns matching text
into emotes or other FFXIV text commands.

- [Setup and usage guide](docs/PuppetMaster.md)
- Configure independent reactions with normal or regex triggers.
- Restrict reactions by official or custom chat channel.
- Control commands with per-reaction allowed and denied lists.
- Choose queue-all, ignore, queue-latest, or immediate-restart retrigger behavior.
- Monitor active and queued reactions in a separate read-only visualizer.
- Override progress and suppression notifications per reaction.
- Inspect message logs and create reactions directly from captured messages.
- Open the plugin settings with `/puppetmaster`.
- Enable or disable every reaction with `/puppetmaster on` or
  `/puppetmaster off`.
- Enable or disable one named reaction with `/puppetmaster on <ReactionName>`
  or `/puppetmaster off <ReactionName>`.
- Open the reaction visualizer with `/puppetmaster viz`.

Only enable reactions in channels you trust. Emotes are allowed by default;
other text commands must be explicitly whitelisted unless **Allow all text
commands** is enabled.
