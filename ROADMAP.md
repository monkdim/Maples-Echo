# Maple's Echo — Roadmap

Guiding principles: this is an accessibility surface first — readable callouts
mid-fight beat every other concern. One-way by design (Discord → game). The
window must never fight the player: no focus theft, no eaten clicks, no
settings that silently revert.

## Shipped (unreleased — PR #1, awaiting in-game testing + release packaging)

- **Bug fixes**: slider persistence, live message-cap updates, dead `FontFace`
  setting removed.
- **Discord message edits relayed**: transcription bots that post a partial
  line and edit in the final text now correct on screen, in place.
- **Click-through mode**: the window can ignore the mouse entirely mid-fight.
- **New-message pulse**: background flash on arrival; keyword hits pulse
  stronger, in that keyword's color.
- **Combat mode** (opt-in): auto-applies in combat — larger font, optional
  timestamp hiding, optional last-N-seconds filter; reverts on combat end.
- **Keyword rules**: per-keyword color, whole-word matching (makes "in"/"out"
  usable), enable toggles, first-match-wins priority. Legacy keywords migrate
  automatically.
- **Polish**: `/mapleecho clear` + Clear button, speaker removal, 24-hour
  timestamps, window open-state persistence.
- **QoL**: "N new messages ↓" pill when scrolled up, Ctrl+scroll font resize,
  token-free config export by default, FFXIV starter keyword/glossary pack.

## Next

- **Crisp font rendering** (its own release): replace `SetWindowFontScale`
  bilinear scaling with a real `IFontAtlas` build at the configured size —
  large text is currently blurry, and blur is exactly what an accessibility
  surface can't afford. Font-face selection returns alongside it. Isolated
  release on purpose: it's the only planned work with real regression risk.

## Transcription correction track

Working assumption: **bot-agnostic**. We don't rely on any upstream
custom-vocabulary feature (Scriptly or otherwise) — other groups will run
other bots, so correction lives in the plugin.

- **Stage 1 — remove friction from the manual layer**
  - Right-click a relayed line → "add glossary rule", pre-filled with that
    text. Corrections happen when the failure is seen, not from memory later.
  - Whole-word and regex options on glossary rules (whole-word can reuse the
    keyword matcher).
  - Shareable correction packs — the token-free export (shipped) makes a
    group's accumulated glossary safe to hand to the next group.

- **Stage 2 — phonetic auto-correction**
  - FFXIV vocabulary (ability/callout terms + user's custom words); incoming
    words that don't look like English get matched phonetically (Double
    Metaphone) + by edit distance, and replaced on strong matches only
    ("limit rake" → "limit break" with no hand-written rule).
  - Pure C#, microseconds, slots into `MessageTransform` next to the glossary;
    fully unit-testable.
  - Ships conservative: minimum word length, strong-match thresholds, a subtle
    marker on auto-corrected lines, and a kill switch.

- **Stage 3 — context packs**
  - Per-duty vocabularies activated automatically via the current zone
    (`TerritoryType`), so correction sharpens exactly where callouts are most
    jargon-dense without one giant global dictionary.

## Later / maybe

- Optional sound cue on keyword hits — for hearing users who just can't run
  Discord audio. Needs the game sound-effect id mapping verified in-game
  first; off by default regardless.
- Word-level inline keyword coloring (hard to combine with text wrapping;
  whole-line tint covers most of the value).

## Parked — deliberately not building

- **LLM correction pass**: adds 0.5–2s to a latency-critical pipeline, plus
  cost and privacy. The in-place edit machinery keeps the door open (show raw
  instantly, patch corrected later) if this ever becomes worth revisiting.
- **Own speech-to-text**: a different product; quality and latency live
  upstream.
- **Two-way chat**: one-way is a design principle, not a gap.
- **Multi-channel relay**: one channel is the product.
- **Token encryption at rest**: Dalamud's whole config folder shares the same
  exposure; documentation is the proportionate answer today.
