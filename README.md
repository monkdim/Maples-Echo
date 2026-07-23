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

## ⚠️ For other users — fork this first

> **This repo is configured for one specific person's setup. If you want to use
> Maple's Echo, fork it and run your own copy.**

Please don't install directly from this repository. Fork it, for three concrete
reasons:

1. **You need your own Discord bot token.** One token supports one gateway
   session — sharing a token causes connection conflicts and dropped messages.
   A token also grants access to a bot sitting in someone else's private server.
   Create your own application in the
   [Discord Developer Portal](https://discord.com/developers/applications).
2. **You need your own custom repo URL.** If you install from this repo's
   `pluginmaster.json`, every update pushed here lands on your machine —
   including changes made mid-raid for one person's specific needs. Host your own
   manifest from your fork and control your own updates.
3. **Support stays with your fork.** Issues, tweaks, and glossary entries
   specific to your group belong in your repo.

### What forking does *not* do

**Plugin settings are stored locally, per user, in Dalamud's own config folder.**
Installing this plugin cannot read or modify anyone else's token, colors, or
speaker assignments — that data never leaves each person's machine. Forking is
about **token ownership, update control, and support boundaries**, not about
protecting someone else's configuration, which is already isolated by design.
Installing it will not interfere with the original user, and forking is not a
security measure for something that isn't at risk.

---

## Never commit your bot token

Your bot token goes in **exactly one place: the plugin's Settings window**,
entered at runtime. It is never stored in this repository and must never be
committed. Dalamud stores it **unencrypted** in its local config folder, so:

- Don't share your Dalamud config files.
- Don't screenshot the Settings → Connection panel with the token revealed.
- The config **export/backup blob contains the token** — treat that string as a
  secret and never paste it anywhere public.

---

## What you need

- **XIVLauncher + Dalamud** (this plugin targets Dalamud **API 15**).
- A Discord **bot** you create and control.
- A transcription bot already posting per-speaker messages into a Discord text
  channel. This was built against **Scriptly**, which posts each utterance as a
  **webhook** message under the speaker's name (`[Scriptly] <speaker>`). The
  speaker prefix is configurable, so other transcription bots can work too.

---

## Discord setup (one-time, done by the developer — not the end user)

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

## Install (from a custom repo)

1. In-game, open `/xlsettings` → **Experimental** → **Custom Plugin
   Repositories**, and add the raw URL to your fork's `pluginmaster.json`.
2. Open `/xlplugins`, find **Maple's Echo**, and install.
3. Open the plugin settings (`/mapleecho config`), paste your **bot token** and
   **channel ID** on the Connection tab, and click **Save & Connect**.
4. The status indicator turns **green (Connected)** on success. If it doesn't,
   the status text tells you which of the three failure modes it is (bad token /
   intent not enabled / channel not accessible).

### Commands

- `/mapleecho` — toggle the relay window.
- `/mapleecho config` — open settings.

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
