# Doding Daga Plugins

Add this URL to Dalamud's custom plugin repositories:

```text
https://raw.githubusercontent.com/dodingdaga/DalamudPlugins/main/PuppetMaster.json
```

## Puppet Master

Puppet Master lets trusted chat messages boss your character around. A matching message can make your character wave, pose, perform a short routine, or run another text command you explicitly allow.

[Read the setup and usage guide](docs/PuppetMaster.md).

### What it can do

- React to simple phrases or messages with changing text.
- Listen only to the chat channels you choose.
- Allow safe commands and block commands you do not want.
- Ignore repeated requests, save them for later, keep only the newest, or react again immediately.
- Run several command lines with short waits between them.
- Show incoming messages, waiting reactions, and recent activity inside the plugin.

### Useful commands

```text
/puppetmaster
/puppetmaster on
/puppetmaster off
/puppetmaster on <ReactionName>
/puppetmaster off <ReactionName>
/puppetmaster viz
```

> [!CAUTION]
> Puppet Master can run text commands on your character. Use specific phrases, trusted channels, and allow only the commands you need.

## A brief history

Puppet Master started as a simple way for friends to sync emotes through chat. One `please dance` message in Free Company chat could make every online FC member using Puppet Master dance together, wherever they were in the game.

Over time, it grew beyond emotes. Reactions gained support for other text commands, several command lines, custom chat channels, and waits between steps.

Today, each reaction can have its own trigger, allowed commands, channels, cooldown, and repeat behavior. Message logs, notifications, and the visualizer make it easier to set reactions up and see what they are doing.

## What's next

These are ideas, not promises. Plans may change as they are tested.

- Group selected reactions so they take turns instead of overlapping.
- Export a reaction as a share code that another user can review before enabling.
- Preview matches and waiting activity without sending commands to the game.
- Show clearer counts for ignored, replaced, or discarded requests.
- Add optional limits and different behavior for each person sending requests.
- Let reactions choose from approved alternatives or run a final action when their work is done.
