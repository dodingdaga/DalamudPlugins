# Puppet Master guide

Puppet Master lets trusted chat messages boss your character around—wave, pose, perform a short routine, or run another text command you explicitly allow.

> [!CAUTION]
> A reaction can run text commands on your character. Use specific triggers, trusted channels, and allow only the commands you need.

## Install and open

1. Open Dalamud Settings and go to **Experimental**.
2. Add this custom plugin repository:

   ```text
   https://raw.githubusercontent.com/dodingdaga/DalamudPlugins/main/PuppetMaster.json
   ```

3. Install **Puppet Master** from the Plugin Installer.
4. Run `/puppetmaster` or open the plugin from the Plugin Installer.

Use **Reactions** to add and manage reactions, **Reaction Editor** to configure one, **Settings** to change defaults, and **Logs** to inspect incoming messages.

## Create your first reaction

Start with a simple wave. Once that works, you can decide how much chaos your friends are allowed to cause.

1. Open **Reactions** and select **Add New**.
2. Give the reaction a recognizable name, such as `Please do`.
3. Select it in **Reaction Editor**.
4. Under **Trigger & Test**, enter `please do` as the trigger phrase.
5. Enter `please do wave` as the test message. The generated `/wave` line should have a green check.
6. Under **Channels**, select the channels allowed to activate it.
7. Enable the reaction.

A new reaction is disabled by default. Settings under **Default Command Rules** and **Default Channel Rules** apply only to reactions created afterward.

## Trigger matching

A trigger tells Puppet Master which chat messages deserve a reaction. Specific phrases are safer and less likely to fire by accident.

### Normal triggers

The normal formats are:

```text
<trigger phrase> <command>
<trigger phrase> (<command with arguments>)
```

Examples:

```text
please do wave
please do (wave motion)
```

Do not include the leading `/` in an incoming message. Puppet Master adds it to the generated command. Separate alternative trigger phrases with `|`:

```text
please do|simon says
```

Matching is case-insensitive.

### Commands with arguments

Wrap the entire command and its arguments in parentheses. Puppet Master removes the parentheses and adds the leading `/`.

| Incoming message | Generated command |
| --- | --- |
| `please do wave` | `/wave` |
| `please do (ac Vercure [t])` | `/ac Vercure <t>` |

Use parentheses when the command has spaces or arguments. In incoming messages, write targets with square brackets such as `[t]`; Puppet Master changes them to the angle brackets required by the game.

### Testing

The test panel updates as the reaction settings change:

- Green check — the generated command is allowed.
- Red cross — the generated command is denied.
- **No match** — the test text did not match the trigger.

The summary reports how many commands will run or be blocked. Get everything green before enabling the reaction.

<details>
<summary>Advanced matching with regular expressions</summary>

Enable **Use Regex** when part of the incoming message can change. Text matched by the first pair of parentheses can be reused as `$1`, the second as `$2`, and so on.

Pattern:

```regex
^Random! (.*) rolls? .*?(\d+)\.$
```

Replacement:

```text
/echo $1 rolled $2
```

Use `^` and `$` when the entire message must match the pattern.

</details>

## Command rules

This is where you decide how much control a reaction gets. Puppet Master follows three command rules:

1. Commands under **Denied commands** are always blocked.
2. Emotes are allowed unless they are denied.
3. Other commands must be under **Allowed commands**, unless **Allow all text commands** is enabled.

Rules apply to the command name, not its arguments. For the Vercure example, add `/ac` to **Allowed commands**, not the entire `/ac Vercure <t>` line.

**Denied commands** always take priority. When **Allow all text commands** is enabled, entries under **Allowed commands** are ignored.

> [!WARNING]
> **Allow all text commands** is powerful and a little dangerous. It can permit disruptive commands such as `/logout` unless they are explicitly denied. Use it only with people and channels you trust.

**Motion only for emotes** changes allowed emotes to their `motion` form, which plays the animation without sending the usual emote text to chat.

## Channels

Select at least one trusted channel. Start small; you can always add more later. A reaction with no selected channel cannot activate.

<details>
<summary>Discover and use a custom channel</summary>

1. Open **Logs** and enable message logging.
2. Cause the desired message in game.
3. Find the captured message and its numeric channel ID.
4. If the type is unknown, select **Add custom channel**, then give it a useful name.
5. Return to the reaction's **Channels** section and enable it.

You can also select **Create reaction** on a log entry. The new reaction matches that exact message and starts with only the source channel enabled.

Channel numbers may change after a game or Dalamud update. If a custom channel stops working, use the Logs tab to find its new number.

</details>

## Control how reactions run

Chat can be impatient. These settings decide whether another matching message is ignored, saved for later, or allowed to interrupt what Puppet Master is doing.

### Multiple command steps

For regex reactions, the replacement may contain one command per line. Every line is checked independently against the reaction's command rules.

```text
/wave
/wait 2
/echo done
```

`/wait` pauses Puppet Master before the next line. Each wait can be up to 60 seconds and may use decimals, such as `/wait 0.5` for approximately half a second.

### What running means

Puppet Master knows when it sends a command, but not when the game finishes the command or emote. In the example above, it sends `/wave`, waits about two seconds, then sends `/echo done`. The wait does not confirm that the wave animation finished.

To keep a reaction busy after its final command, add a wait at the end:

```text
/wave
/wait 2
```

Without the final wait, a single-command reaction finishes shortly after sending the command, even if its effect is still visible in the game. Cancelling stops unsent lines and current waits, but it cannot undo a command already sent or stop an animation already playing.

### Cooldown and repeat behavior

While a reaction is busy, another instance of that same reaction cannot start. Different reactions can still overlap.

**Cooldown** is the minimum time between starts. With a 10-second cooldown, a reaction starting at `00:00` cannot start again before `00:10`. It must also finish its current run before the next one can start.

### When another message arrives

| If another message arrives while the reaction is busy… | Choose |
| --- | --- |
| Ignore it | **Ignore** |
| Run every request afterward | **Queue every trigger** |
| Keep only the newest request | **Queue latest trigger** |
| Stop the remaining steps and react again immediately | **Restart immediately** |

Use **Restart immediately** for short reactions that should respond again right away. Avoid it for long multi-line reactions because a new message can stop the remaining lines, and cooldown does not apply.

<details>
<summary>More about cooldowns and waiting requests</summary>

- Cooldown starts when a reaction starts.
- If the reaction is still busy when cooldown ends, a waiting request remains waiting until the current run finishes.
- **Queue every trigger** and **Queue latest trigger** save new requests only while the reaction is busy. A new message received after the reaction finishes but before cooldown ends is ignored.
- **Restart immediately** stops unsent lines, clears older waiting requests, and starts the newest request without cooldown.
- Disabling, deleting, or changing a reaction stops its current wait and clears its waiting requests.

</details>

### Avoiding reaction loops

A reaction that replies to itself can loop forever—funny once, less funny when you cannot stop waving. For example, a reaction that responds to `loop` with `/echo loop` will keep triggering itself.

To stop all reactions immediately, run:

```text
/puppetmaster off
```

This also clears requests that are waiting. The notification's **Cancel** button stops only the current run, so it may not stop a loop when another request is waiting. Before enabling a reaction, check whether its commands can create a message that matches the same reaction.

## Notifications

Under **Settings → Notifications**, configure the global defaults:

- **Show reaction progress notifications** displays start, step progress, completion, cancellation, and the Cancel button.
- **Notify when a reaction is suppressed** occasionally tells you when a message was ignored because the reaction was busy or cooling down.

“Completed” means Puppet Master finished working through the configured command lines. It does not mean the game or another plugin finished them.

<details>
<summary>Per-reaction notification behavior</summary>

Each reaction can **Use global default**, **Always show**, or **Never show** for both notification types. Global changes apply to later triggers; a reaction already running keeps the behavior captured when it started.

</details>

## Reaction visualizer

Select **Open Visualizer** in the Reactions tab, or run:

```text
/puppetmaster viz
```

The read-only visualizer lets you watch everything unfold. It shows reactions that are running, waiting, or recently finished. Hover an item to see its command and time.

A completed item means Puppet Master finished working through its command lines. It does not confirm that the game finished the commands or animations.

## Message logs

When a trigger mysteriously refuses to work, the **Logs** tab is usually the best place to look. It captures messages inside the plugin UI, can help identify unknown channels, and can create a reaction directly from a message.

Commands:

```text
/puppetmaster logging on
/puppetmaster logging off
/puppetmaster logging clear
/puppetmaster logging save
```

Logging is disabled when the plugin starts and must be enabled for the current session. The save command reports the full output path in game chat.

<details>
<summary>Waiting-request limits</summary>

During extreme message spam, Puppet Master may discard older messages that are still waiting. **Queue every trigger** keeps up to 16 waiting requests for each reaction. The Logs tab shows how many messages or requests were discarded.

</details>

## Managing reactions and commands

Reactions may share a name. Name-based commands affect every exact, case-sensitive match, but those reactions can still run at the same time.

```text
/puppetmaster
/puppetmaster on
/puppetmaster off
/puppetmaster on <ReactionName>
/puppetmaster off <ReactionName>
```


## Configuration upgrades and recovery

<details>
<summary>Backups when settings are upgraded</summary>

When Puppet Master upgrades settings from an older version, it first creates a dated backup. If the upgrade fails, the backup is kept so the previous settings can be recovered.

</details>

## Troubleshooting

### The test says No match

- Confirm that the test message contains the trigger followed by a command.
- Put commands with spaces inside parentheses for a normal trigger.
- For regex, check the pattern, `^` and `$`, and references such as `$1` or `$2`.

### The generated command has a red cross

- Check whether it appears under **Denied commands**.
- Emotes are otherwise allowed automatically.
- Add a non-emote command to **Allowed commands**, or carefully enable **Allow all text commands**.

### The test works but nothing happens in game

- Enable the reaction.
- Select at least one correct channel.
- Confirm the reaction is not already busy or still cooling down.
- Enable logging and verify the actual incoming channel ID.
- Check the progress/suppression notifications and Dalamud plugin log for errors.

### A reaction runs too often

- Use a longer trigger phrase or anchor a regex with `^` and `$`.
- Restrict the enabled channels.
- Add a cooldown.
- Choose **Ignore** or **Queue latest trigger** instead of queueing every trigger.
- Do not use **Restart immediately** when cooldown should control how often the reaction starts.

### A custom-channel reaction stopped working

- Enable logging and rediscover the channel ID.
- Update the custom channel and the reaction's selected channels.

### Where is the configuration?

Dalamud stores it in the XIVLauncher plugin configuration directory. Avoid editing the active file while the plugin is running. Keep the automatic backup if you may need to restore older settings.
