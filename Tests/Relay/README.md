# Historical preview verification

Run the full solution with .NET 10 and a Dalamud API 15 development install:

```powershell
dotnet test MaplesEcho.slnx -c Release
```

The relay tests reference the actual plugin and mock Discord messages and the
framework scheduler. They use the default Windows Dalamud development location,
or DALAMUD_HOME. The existing pure transform/store tests remain independent of
Dalamud and can still run with their own project.

Assertions cover original creation timestamps, shared glossary/markdown/embed
transformation, channel/webhook filtering, distinct fetched/matching/readable
counts, repeated history reads, empty/failing history, and live arrival during
an outstanding preview. Live store, last-live timestamp, status, speaker callbacks,
and arrival callbacks must not change as a result of history. Arrival callbacks
are the boundary used by native chat mirroring and keyword sound.

The live regression test exercises the actual builder/queue and store edit path;
it does not simulate the Discord socket event dispatcher or native game audio.

## In-game check (requires a running game and test Discord channel)

1. Post a keyword callout such as "spread"; wait at least ten minutes.
2. Enable keyword sound and native chat mirroring. Set a short combat freshness
   filter, if available during the test.
3. In settings, click "Preview last 5". The old message must appear only under
   "Historical preview", with its original date/time.
4. Repeat the preview. Confirm no combat-window insertion, arrival pulse, keyword
   sound, chat mirror, or reset of the last-live-message age.
5. Post a fresh "stack". Confirm exactly one normal live arrival. Edit it and
   confirm in-place text replacement.
6. Test an empty channel and denied history access. Settings must explain the
   outcome without reporting live recovery. Record both settings and the relay
   window to check these observations.

The automated tests do not establish in-game readability, pulse behavior,
audibility, or performance; those observations remain a manual verification step.

## Welcome and changelog

The welcome screen uses the plugin assembly version. For each release, bump the
project version as usual and update Services/ReleaseNotes.cs with that release's
player-facing changes. The local acknowledgement is excluded from shared packs.

In-game verification:

1. Load with no LastSeenWelcomeVersion: welcome should appear outside combat
   and cutscenes, without taking focus.
2. Dismiss with Got it or the title-bar close button, reload, and verify it
   stays closed. Open settings from the welcome window and check the same.
3. Reopen from settings and /mapleecho news.
4. Set the saved version lower than the current assembly version and reload:
   verify it appears again. A saved newer version must not reopen on downgrade.
5. Reload during combat or a cutscene and verify the window waits until both end.
6. Resize the welcome window to its minimum width and increase the game UI font
   scale. Check that all three rounded cards wrap without overlap, buttons stack,
   and the footer can be reached by scrolling. The Viera illustration is static.
   Check the cream/rose text against the opaque plum background on your monitor.
7. Confirm the Viera badge has transparent edges, intact ears and speech bubble,
   and keeps its proportions at different UI scales. Reopen the window after
   leaving it closed for a while to check asynchronous texture reload.
