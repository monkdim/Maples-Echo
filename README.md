# Maple's Echo

**Maple's Echo is an accessibility plugin that brings a Discord channel's live
voice-to-text callouts into Final Fantasy XIV**, so anyone who can't rely on the
voice channel can read what the party is calling out without alt-tabbing away
mid-fight. It shows those messages in a dedicated, fully customizable in-game
window — colors, font size, spacing, and per-speaker colors are all yours to
tune.

It is one-way by design: **Discord → game**. A third-party transcription bot
listens to your voice channel and posts each utterance as a message; this plugin
relays those messages into the game.

> In FFXIV lore, the Echo is a gift that lets its bearer understand speech they
> otherwise could not. That is exactly what this plugin is for.

---

## Features

**The relay window**
- Crisp text at any size — fonts are built at your configured pixel size, not
  blur-scaled. Resize on the fly with **Ctrl+scroll** over the window.
- Per-speaker colors (auto-assigned, all overridable), timestamps (12/24-hour),
  merging of consecutive lines from the same speaker.
- A brief **background flash** when a message lands — the visual stand-in for
  hearing someone start talking. The window never steals focus or input.
- **Click-through mode**: the window ignores the mouse entirely so clicks and
  camera drags pass through to the game mid-fight.
- Scrolled up to re-read? A **"N new messages ↓"** pill counts what's arriving
  below; click it to jump back down.
- Failure is never silent: red background tint on disconnect, a **Reconnect**
  button right in the window, an automatic **watchdog** that restarts a relay
  stuck after PC sleep or a network change, and (on by default) a **notice in
  the native chat log** when a working relay dies or recovers — so you know
  even when the window is closed.
- Transcription bots that post a partial line and then *edit* in the final text
  are handled: the line **corrects itself in place**.

**Keywords** — emphasize the words that matter ("stack", "spread", "in",
"out", your name). Each keyword gets its own color used for the line, the
marker, and the flash; whole-word matching keeps "in" from lighting up
"point"; first match in the list wins, and rules reorder with ▲▼. Optional
**chat sound ping** (`<se.1>`–`<se.16>`) on keyword hits, for hearing users
who can't run Discord audio.

**Glossary** — find → replace rules that fix common mistranscriptions before
display, with whole-word and regex options. **Right-click any relayed line**
to copy it or turn it into a glossary rule on the spot.

**Combat mode** (opt-in) — while in combat the window can automatically switch
profiles: bigger text, hidden timestamps, only the last N seconds of messages.
Reverts the moment combat ends.

**Presets & sharing** — one-click appearance presets, a curated FFXIV starter
pack for keywords and glossary (with a **colorblind-friendly palette**
variant), and a config export that is **token-free by default**, so a tuned
setup is safe to share with another player. Contrast ratios are checked live
against WCAG targets as you pick colors.

---

## Installing

Add this custom plugin repository in-game, then install from the plugin list —
**no fork and no GitHub account needed.** The plugin ships generic; you set up
your own Discord bot and channel in its settings, stored privately on your own
machine.

1. In-game, open `/xlsettings` → **Experimental** → **Custom Plugin
   Repositories**, and paste this URL:

   ```
   https://raw.githubusercontent.com/monkdim/maples-echo/MaplesEchoV1/repo/pluginmaster.json
   ```

2. Open `/xlplugins`, search for **Maple's Echo**, and install.
3. Open settings with `/mapleecho config`, paste your **bot token** and **channel
   ID** (see *Discord setup* below) on the Connection tab, and click **Save &
   Connect**. The status turns **green (Connected)** on success; if not, the
   status text names the failure (bad token / intent not enabled / channel not
   accessible).

### You still need your own Discord bot token

You don't fork anything, but you do need **your own bot**: one bot token supports
one gateway connection, so everyone runs their own. Creating it is a few minutes,
one time (see *Discord setup*). The token is entered in-game and never leaves your
machine.

### Your settings are private to you

Plugin settings — token, colors, speaker assignments, glossary — are stored
locally, per user, in Dalamud's own config folder. Installing this plugin cannot
read or modify anyone else's configuration, and yours cannot affect theirs.

---

## Keep your bot token private

Your bot token goes in **exactly one place: the plugin's Settings window**,
entered at runtime — never in a file or this repository. Dalamud stores it
**unencrypted** in its local config folder, so:

- Don't share your Dalamud config files.
- Don't screenshot the Settings → Connection panel with the token revealed.
- The config export is **token-free by default** and safe to share as a
  settings pack. If you tick **Include bot token** when exporting, treat that
  string as a secret and never paste it anywhere public.

---

## What you need

- **XIVLauncher + Dalamud** (this plugin targets Dalamud **API 15**).
- A Discord **bot** you create and control.
- A transcription bot already posting per-speaker messages into a Discord text
  channel. This was built against **Scriptly**, which posts each utterance as a
  **webhook** message under the speaker's name (`[Scriptly] <speaker>`). The
  speaker prefix is configurable, so other transcription bots can work too.

---

## Discord setup (one-time)

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
   and **create a new application**.
2. Under **Bot**, add a bot.
3. **Enable the Message Content privileged intent** on the Bot page. This is the
   single most common silent failure: without it, messages arrive with empty
   content and nothing shows up, with no error.
4. **Invite the bot** to your server with at minimum **View Channel** and
   **Read Message History** permissions on the target channel.
5. In Discord, enable **Developer Mode** (User Settings → Advanced), then
   right-click the channel → **Copy Channel ID**.
6. Copy the **bot token** from the Bot page (you'll paste it into the plugin,
   never into a file).

---

## Commands

- `/mapleecho` — toggle the relay window.
- `/mapleecho config` — open settings.
- `/mapleecho clear` — clear the relayed messages (e.g. between pulls).

---

## Building from source

Requires the .NET 10 SDK and a local Dalamud dev install (XIVLauncher provides
one). The project uses `Dalamud.NET.Sdk`, which locates Dalamud via the
`DALAMUD_HOME` environment variable or the default XIVLauncher path.

```bash
# Point at your Dalamud dev distribution if it isn't in the default location:
#   export DALAMUD_HOME="$HOME/.xlcore/dalamud/Hooks/dev"   # Linux/XIVLauncher.Core
#   set DALAMUD_HOME=%AppData%\XIVLauncher\addon\Hooks\dev  # Windows

dotnet build -c Release        # builds MaplesEcho.dll and packages latest.zip
dotnet test                    # runs the transform/contrast unit tests
```

The Release build produces `MaplesEcho/bin/Release/MaplesEcho/latest.zip`, ready
to attach to a GitHub Release. Bump both the `<Version>`/`<AssemblyVersion>` in
`MaplesEcho/MaplesEcho.csproj` **and** the `AssemblyVersion` in
`repo/pluginmaster.json` on every release, or updates won't be offered.

### Test builds

The repo carries a **testing channel** alongside stable: `repo/testing.zip` +
`TestingAssemblyVersion` in `repo/pluginmaster.json`. Testers opt in per plugin
(right-click Maple's Echo in `/xlplugins` → **Receive plugin testing versions**;
enable testing builds under `/xlsettings` → Experimental if the option isn't
shown) and get the testing build; everyone else stays on stable. To cut a test
build: bump the csproj version, `dotnet build -c Release`, copy `latest.zip` to
`repo/testing.zip`, and set `TestingAssemblyVersion` to match. Promoting to
stable is then just replacing `repo/latest.zip` and `AssemblyVersion` with the
same bits.

For rapid local iteration, skip packaging entirely: add the build output folder
(`MaplesEcho/bin/Release/MaplesEcho/`) as a **Dev Plugin Location** under
`/xlsettings` → Experimental.

---

## A note on latency

Voice → text → Discord → gateway → display carries real cumulative latency,
typically a couple of seconds. That's upstream of this plugin and can't be
engineered away. The practical fix is social: call mechanics **a beat earlier**
than a hearing party would.

---

## Legal

Dalamud is third-party software, and using it is against Square Enix's Terms of
Service. This is provided so you can make that decision informed.
