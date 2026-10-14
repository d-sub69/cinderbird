# Cinderbird performance test

A repeatable check against the performance budget (`Assets/_Project/Data/PerformanceBudget.asset`).
Run it after any change that could cost frame time, and on any new PC.

## Budget

Measured on the development PC (Ryzen 7 5700X, RTX 3070 Ti, 32 GB) at 1080p, **Medium**, **VSync off**.
Staying inside it there should keep the minimum spec (GTX 1050, i5-6500, 8 GB RAM) at 60 fps at 1080p Medium.

| Figure (F3 overlay) | Budget | Meaning |
|---|---|---|
| CPU (avg and p95) | ≤ 6 ms | Main-thread work per frame, without waiting for VSync or the GPU |
| GPU (avg and p95) | ≤ 4 ms | GPU time per frame |
| GC | 0 B/frame | Managed garbage per frame in normal play (worst frame of each refresh) |
| Dig | ≤ 4 ms | One scoop on the main thread: taking its ash and embers (the hole then drains open in settling updates) |
| Settle | ≤ 4 ms | One settling update on the main thread: writing the sliding ash and starting its re-mesh (`settle_max`), plus uploading the new meshes a frame later (`upload_max`). Each must stay under the budget, and leave room in the 6 ms CPU budget |
| Save | ≤ 2 ms | Main-thread part of an autosave |
| Start | ≤ 3 s | Scene start to player control (build mountain + load save) |

Red in the overlay = over budget. "p95" is the time 95% of frames stay under; it catches stutter an average hides.

## Setup

1. **Cinderbird → Build → Windows - Performance Test** (frame rates: a release build with the debug keys and the
   F3 overlay on) and **Windows - Development** (garbage figure, Profiler). Builds land in `Builds/` next to
   `Assets/`. The plain **Windows - Release** build is what players get: no debug keys, no overlay, no Test Mode.
2. In the game: Esc → Options → Display mode **Windowed**, Resolution **1920 × 1080**, VSync **off**,
   Frame rate limit **Unlimited**, Graphics quality **Medium**. Back, Resume.
3. Close other heavy programs (browser video, other games, Unity itself for the Performance Test build).
4. F3 shows the overlay (on by default); keep it on, the log lines are only written while it shows.

## Route (about 3 minutes)

Start each run with **F8** (fresh mountain) so runs are comparable, then:

| # | Do | Shows up in the log as |
|---|---|---|
| 1 | Wait until the loading screen is gone. | start |
| 2 | Stand still facing the mountain for 10 s. | FPS, frame p95, CPU, GPU, GC |
| 3 | Turn slowly 360° on the spot. | worst CPU / GPU p95 |
| 4 | Walk to the pile, scoop 20 times in one spot (dig a deep hole), shaking between scoops. | Dig (highest seen), GC while shaking |
| 5 | Walk once around the mountain's foot. | CPU, GPU p95 |
| 6 | Press **F5** (save) three times, a few seconds apart. | Save |
| 7 | Press **F9** (reload, now with a save to load) and wait 10 s. | start |
| 8 | Repeat 2 on **High** and **Low** quality (optional). | GPU |

Run the route in the **Performance Test** build for frame times, then steps 2 and 4 in the **Development** build for the
**gc_worst** figure (Development builds run a bit slower, and release builds may not count garbage).

## Results

No copying needed: while the F3 overlay is shown, the game writes one `[Perf]` line to its log every 5 seconds,
covering those 5 seconds:

```
[Perf] t=35s Medium vsync=off 1920x1080 | fps=142 frame=7.0 p95=8.3 worst=15.2 | cpu=4.1 p95=5.0 | gpu=3.2 p95=3.6 | gc_worst=0B gc_frames=0 | digs=3 dig_max=2.8ms mesh_max=2.5ms chunks_max=1 after_dig_max=0.4ms | saves=0 save_max=0.0ms | start=2.14s
```

Logs:
- Build: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Cinderbird\Player.log` (overwritten at each start, so
  read it before starting the game again; the previous run is kept as `Player-prev.log`).
- Editor: the Console, and `%LOCALAPPDATA%\Unity\Editor\Editor.log`.

Play the route above, quit, and the lines tell the story step by step: `start` for steps 1 and 7, `digs`,
`dig_max` (whole dig), `mesh_max` (its re-meshing part) and `after_dig_max` (glimmers, effects and HUD reacting to
the dig) for step 4, `saves` and `save_max` for step 6; `gc_frames` counts frames that allocated anything. A line marked `(paused/loading)` includes frames from the
loading screen or the pause menu; ignore it for frame times.

If a figure is over budget, open the Profiler (Window → Analysis → Profiler, attach to the Development build) at
that step; the game's own markers are `Cinderbird.Dig`, `Cinderbird.Mesh` and `Cinderbird.Save`.
