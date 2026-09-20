# Backlog: good to great

Not committed to, not sequenced by sprint — a ranked list of what would make this app
memorable instead of merely correct. Written 2026-09-18, informed by
[PRODUCTIONIZATION_ASSESSMENT.md](PRODUCTIONIZATION_ASSESSMENT.md) (which covers
deployability and correctness, not features) and the current pipeline in
[ARCHITECTURE.md](ARCHITECTURE.md).

Effort is rough and assumes the current stack (ffmpeg via argument lists, no database,
Clean Architecture boundaries). Each item names the tradeoff it would introduce, not just
the upside — several of these are in tension with the "render one rotation and loop"
trick that makes render time length-independent, which is worth protecting on purpose
rather than losing by accident.

## Tier 1 — the actual wow gap

**The record currently doesn't listen to the music.** It spins at a fixed operator-chosen
speed. A "DJ Visualizer" whose visual has no relationship to the audio is the single
biggest gap between what this app does and what the name promises.

### 1. Audio-reactive motion
Amplitude-driven pulse on the record (subtle scale/glow bump on transients) and/or a
beat-synced ring around the artwork. This is the feature that makes someone's first
render make them say "wait, how" instead of "oh, a spinning image."

- **Why it's worth the cost:** it's the difference between a template and an instrument.
  Everything else in this backlog is polish around a static core; this changes the core.
- **The real tradeoff:** breaks the one-rotation-loop optimization directly. If the visual
  depends on audio at time *t*, you can no longer render one period and `-c:v copy` it
  across the rest of the track — render time goes back to being proportional to length.
  The fix is not "give up the trick," it's **decouple the two speeds**: render the
  spinning-record layer once as today (cheap), and composite a *separate*, genuinely
  per-frame reactive layer (a thin glow ring or waveform strip) on top, driven by an
  amplitude envelope computed once via `ffmpeg -af astats`/a downsampled RMS pass — not
  by re-rendering the whole frame. That keeps the expensive part length-independent and
  makes only the cheap overlay length-proportional.
- **Effort:** L. Needs an amplitude-extraction step in the render pipeline (new
  Infrastructure port, e.g. `IAudioEnvelopeExtractor`), a new ffmpeg filter graph, and
  Domain concepts for what "reactive" means (a threshold? a curve?). Real design work,
  not a filter tweak.

### 2. Live in-browser preview before committing to a render
Right now the only way to see the output is to upload, wait, and download. A canvas
preview — decode the audio with the Web Audio API, draw the spinning artwork with
`requestAnimationFrame`, let the user scrub the rotation-speed slider and *see* it spin
in real time — turns "upload and hope" into "adjust and watch."

- **Why it's worth it:** it's the fastest way to make the tool feel responsive, and it's
  pure frontend — zero backend risk, zero change to the render pipeline or its
  guarantees.
- **Tradeoff:** the preview and the real render are two independent implementations of
  "what a spinning record looks like" (canvas 2D vs. ffmpeg filter graph). They will
  drift unless the rotation math (degrees per frame from `rotationSpeedSeconds`) is
  written once and shared — e.g. as a small pure function importable by both a canvas
  component and whatever generates the filter-graph expression, with a test asserting
  they agree at a few sample times.
- **Effort:** M. No backend change required at all for a first version (preview only the
  spin + static artwork; the ambient blur glow can stay render-only).

## Tier 2 — reach, not just polish

### 3. Vertical and square export presets
Add 9:16 and 1:1 alongside the current 1080p/720p landscape. A DJ mix video's natural
home now is Reels/TikTok/Shorts, not a 16:9 player.

- **Why it matters:** this is a distribution multiplier, not a cosmetic option — it's the
  difference between "a video file" and "a video that fits where people actually post
  mixes."
- **Tradeoff:** the current layout (record centered, title along the bottom) was designed
  for landscape headroom. Vertical needs its own composition, not a crop of the
  landscape one — likely a second filter-graph template in the render use case, not a
  parameter on the existing one.
- **Effort:** M.

### 4. Track markers / a mix tracklist overlay
DJ mixes are usually multiple songs. Let the uploader paste or upload a simple tracklist
(`00:00 Track A`, `04:32 Track B`, ...) and render it as a title card that changes at each
timestamp, instead of one static title for the whole mix.

- **Why it matters:** this is the feature every actual DJ asks for the first time they use
  a tool like this. It's also a natural home for future YouTube-chapter-marker export.
- **Tradeoff:** title changes mid-video mean the caption can no longer be a single
  `textfile=` burned into the one rendered rotation period (see Tier 1 tradeoff above) —
  the caption becomes genuinely time-varying, so it has to be composited as its own
  length-proportional overlay, same shape as the audio-reactive layer. Worth building
  the two together since they share the "separate reactive overlay track" mechanism.
- **Effort:** M–L, mostly in the Domain (parsing/validating a tracklist) and a new
  Application use case; ffmpeg-side it's drawtext with `enable='between(t,a,b)'` per
  entry.

### 5. A second visual theme
Right now there is one look: circular artwork, blurred glow background, title below. A
second theme (e.g. a cassette-tape reel, or a flat album-grid style with a progress bar)
picked from the same radio-button pattern as preset/font today.

- **Why it matters:** variety is what turns "I used a tool" into "I found a look."
- **Tradeoff:** the render use case currently builds one filter graph. A second theme
  means a `VideoTheme` selection that maps to a *different* filter graph, not a branch
  inside the existing one — otherwise the existing graph accretes conditionals until it's
  unreadable and untestable independently.
- **Effort:** M, assuming the filter-graph-per-theme structure is kept clean from the
  start.

## Tier 3 — worth doing, not urgent

### 6. Websocket/SSE progress instead of polling
`ProgressPanel` polls job status today. Server-sent events would make progress feel
instant rather than snapped-to-interval, and cut request volume on the free-tier demo.

- **Tradeoff:** the render worker is a separate process from the API (`FileSystemJobStore`
  is the only channel between them). SSE needs the API to notice a `status.json` change
  without polling *it* either — a `FileSystemWatcher` on the job directory, feeding
  connected clients. Real but contained; doesn't touch the render pipeline.
- **Effort:** M.

### 7. GPU-accelerated encode (NVENC/QSV) where available
Detect hardware encode support and use it, falling back to libx264 — meaningfully faster
renders on a machine that has it, free.

- **Tradeoff:** another code path to keep correct across dev/CI/deploy, and CI/Render's
  free tier have no GPU to test against — this would only ever be exercised on a
  self-hosted box, so it needs its own test seam (an `IEncoderSelector` a test can fake)
  rather than being verified by the existing render tests.
- **Effort:** S–M for detection + fallback; the risk is entirely in "did this actually get
  exercised," not the ffmpeg args themselves.

### 8. Batch upload (queue several mixes in one visit)
Multiple audio files in one form submission, rendered as separate jobs the user can watch
progress on together.

- **Tradeoff:** direct tension with the demo's abuse-resistance design (rate limiting,
  small caps, short retention exist specifically so the free demo can't become a public
  transcoder — see [SECURITY.md](SECURITY.md)). This is a self-hosted-only feature unless
  the demo's rate limiter is extended to account for "N jobs in one request" rather than
  one job per request.
- **Effort:** M, mostly in tightening the rate limiter's unit of accounting before adding
  the feature, not after.

## Explicitly not on this list

- **Accounts, saved projects, a history of past renders.** Cuts directly against the
  documented "nothing kept" design (no database, no auth, deliberate — see
  [ARCHITECTURE.md](ARCHITECTURE.md)). Would double the attack surface for a feature
  nobody has asked for; revisit only if a real user asks for it, not preemptively.
- **AI-generated artwork/backgrounds.** Interesting, but it's a different product (an
  image generator) wearing this one's UI. Would also reintroduce exactly the "what if the
  input is malicious" surface this app worked to remove.

## If picking just one

**#1 (audio-reactive motion) if the goal is a portfolio piece that gets remembered.**
**#2 (live preview) if the goal is the best return per hour spent**, since it's pure
frontend, has no interaction with the render pipeline's guarantees, and is the fastest
way to make the tool feel alive during the several seconds a render still takes.

---

# Backlog: premium feel

A different lens than the tiers above. Those are features; this is craft — closing the
gap between "correctly implements a spinning-record generator" and "feels like it cost
money." Every item below is grounded in what the current components actually do (read
2026-09-20): [ProgressPanel.tsx](../frontend/src/components/ProgressPanel.tsx),
[UploadCard.tsx](../frontend/src/components/UploadCard.tsx),
[FilePreview.tsx](../frontend/src/components/FilePreview.tsx),
[DownloadPanel.tsx](../frontend/src/components/DownloadPanel.tsx), and
[RenderSettings.tsx](../frontend/src/components/RenderSettings.tsx). None of it touches
the render pipeline or its guarantees — this is entirely frontend, entirely reversible,
and safe to do incrementally.

### 1. Play the result, don't just link to it
`DownloadPanel` today is a heading and a bare `<a download>`. The payoff moment of the
entire flow — the video you waited for — is never actually shown in the app. Add an
inline `<video controls>` with the job's own output as the source (a poster frame taken
from the first rendered rotation frame), download button alongside it rather than
instead of it.
- **Why it matters most:** this is the single highest-leverage change in the whole
  backlog. It's the difference between "here's a file" and "here's your video" — and it's
  almost pure UI, no backend change (the download URL already exists and already streams
  the file; a `<video>` tag can point at the same URL an `<a>` does today).
- **Effort:** S.

### 2. Kill the "is this frozen?" moment
This is the exact thing that just happened: a static "Queued — waiting for a worker to
pick this up" with a bar frozen at 0% reads as broken, not as waiting. Give `Queued` an
indeterminate animated state (a sweeping gradient or pulsing dot, not a percentage that
implies precision it doesn't have yet), and update the document title
(`document.title`) with live progress (`"73% · DJ Visualizer"`) so a backgrounded tab
communicates status without being watched.
- **Why it matters:** perceived responsiveness is most of what "premium" *is* — a tool
  that visibly acknowledges it's working is trusted more than one that's actually faster
  but looks idle.
- **Effort:** S. Respect `prefers-reduced-motion` on the animation.

### 3. Make the controls look designed, not defaulted
`RenderSettings` uses bare native `<input type="radio">` and `<input type="range">` —
functional, but it's the first thing Tailwind's own docs tell you to replace. Two
specific upgrades: a segmented-control look for preset/font (the font options especially
— render each label in its *own* actual font, using the same Poppins/Abril
Fatface/Space Mono files already bundled in `assets/fonts/`, so the choice previews
itself instead of naming itself), and a custom-styled range thumb/track for rotation
speed that matches the app's palette instead of the OS default.
- **Why it matters:** these are the controls a user touches before ever seeing output —
  they set the quality expectation for everything that follows.
- **Effort:** S–M. Pure CSS for the slider; the font-preview radios need the fonts
  loaded as `@font-face` in the frontend (they're currently only consumed by ffmpeg,
  never shipped to the browser).

### 4. Real drag-and-drop feedback
`UploadCard`'s dropzone only changes on `:hover`; there's no visual response to a file
actually being dragged over it (`dragenter`/`dragleave` are unhandled), and a non-image
file shows a generic "FILE" box with no icon differentiation. Add an active
drag-over state (border/background shift the instant a file enters the zone) and
per-type icons or a small waveform glyph for audio.
- **Why it matters:** drag-and-drop is the first interaction most users have with the
  app. Silence during it (no visual acknowledgment that the drop *will* register) reads
  as unfinished.
- **Effort:** S.

### 5. A waveform, not just a filename
Once an audio file is selected, `FilePreview` shows a name and a byte count — the same
treatment as the image. Decode a downsampled peak overview client-side (Web Audio API's
`decodeAudioData`, no upload needed) and draw a small static waveform strip. Confirms at
a glance that the right file was picked and that it isn't silence or corrupted, before
committing to the upload and the wait.
- **Why it matters:** it's the same instinct as a poster frame on a video — proof the
  input was understood, before the expensive part starts.
- **Effort:** M. Client-side only; large files should decode a bounded byte range or run
  the decode in a Web Worker so it doesn't block the main thread on a 2 GB file.

### 6. A completion moment that feels like one
The instant a render finishes today, `DownloadPanel` mounts as a plain paragraph. The
end of the flow is the one moment worth a flourish — a brief fade/scale-in on the
video and download button (150–250ms, respecting `prefers-reduced-motion`) turns
"the state changed" into "it's done" in a way a hard swap doesn't.
- **Why it matters:** this is the last thing a user experiences per visit — it's what
  they remember the tool as.
- **Effort:** S.

### 7. A voice, not just status strings
"Checking status...", "Something went wrong. Please try again.", "Could not check the
job status. Retrying..." are placeholder-grade — accurate, but interchangeable with any
other app's. A short copy pass (still terse, still no false cheerfulness on a real
error) gives the tool a distinct personality in the ten or so strings a user actually
reads.
- **Why it matters:** cheap, and copy is the one polish item with zero design or
  engineering dependency — it can happen independently of everything else here.
- **Effort:** S.

### 8. An elevation system, not `bg-white/5` everywhere
Every card, input, and panel currently uses the same flat `border-white/20 bg-white/5`
treatment. A small, deliberate set of 2–3 surface levels (base, raised, raised+focused)
with consistent shadow/border tokens would read as a designed system rather than
Tailwind's un-opinionated defaults left as-is.
- **Why it matters:** this is the difference a trained eye clocks in the first second,
  even if they can't say why.
- **Effort:** M, and worth doing *after* items 1–4 rather than before — it's a visual
  language change, better applied once to real components than twice because the
  drag-state and font-preview work above will touch the same markup.

## If picking just one, here too

**#1 (play the result inline).** It's the smallest change on this list and the one that
turns the app from "a file processor with a form in front of it" into something that
feels finished — you can *see* what you made without leaving the page.
