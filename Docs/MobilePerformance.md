# Full-density mobile performance pass

Base: receipt UI commit `3805d3b`. Food counts, spawning, inventory/Stock, cooking rules, ECS motion, particle counts and all 435 food kinds are unchanged. PC keeps detailed meshes. Mobile uses the existing lower-tessellation mesh variant: all-kind mesh triangle total 1,646,968 -> 823,484. This is mesh detail, not fewer food instances. Contact shadows and effects remain.

Changes:
- Cache preview recipe lookup until input, IME composition, player, phase, cookbook state or match epoch changes. Submission and composition guards are untouched.
- Format scores/timer only when displayed numbers change.
- Cache pointer coordinates once per frame; skip passive hover tests on mobile without an active touch. All food still renders/animates.
- Export the existing frame profiler at the end of the nine-command smoke scenario.
- Fix the diagnostic screenshot path on Android, where Unity prefixes persistentDataPath itself.

## Measured comparisons
One exploratory run per condition, same nine-command smoke workload; no concurrent Unity build during reported samples. Frame distributions are frame times, not isolated GPU timings. View CPU includes job dependency waiting; effects CPU excludes its Update work. Short tests and frame pacing are not a production benchmark.

| Environment | Before median / p95 frame ms | After median / p95 frame ms | View CPU median before -> after | GC collections before -> after |
|---|---|---|---|---|
| Mac M3 Pro, 390×844, desktop detail unchanged | 16.67 / 24.35 | 8.66 / 17.39 | 5.80 -> 5.38 ms | 151 -> 7 |
| Android 15 ARM64 emulator, SwiftShader software GPU, 1080×2220 | 733.33 / 833.33 | 466.67 / 550.00 | 3.46 -> 1.50 ms | 18 -> 6 |

Android used identical 37 sampled frames, maximum 10,110 rendered food and 10,132 ECS visuals, 8 successful cooking commands and final scores 44,010 / 33,160 in both runs. All requested commands still run; one fails identically at the very low simulated frame rate. Median frame time improved about 36%, but roughly 2 FPS is **still not playable** and does not estimate real-phone performance. The emulator uses software rendering because its host graphics mode did not satisfy Unity's graphics requirements.

Mac maximum ECS visuals were 10,133 before and 10,134 after (normal frame-timing variation), with nine successful commands in both. A separate Mac run forcing `-feast-mobile-rendering` retained maximum 10,134 ECS visuals and measured 3.17 ms median View CPU; it is not an Android device benchmark.

The original Android comparison smoke reports a failure only because the diagnostic screenshot was incorrectly given an absolute Android path. Its frame statistics were written and the gameplay continued; this path issue is fixed in the final source. Do not call those original runs clean functional passes.

## Validation and reproduction
- `MobileGeometryTests.Validate`: all 435 kinds nonempty, reduced/equal triangle count; passes.
- Existing simulation 1,495 checks, mesh/audio tests, Mac/Android builds, mocked WebGL input tests pass.
- Shared UI event-flow regression: 13 events, zero runtime errors, saves disabled; passes with mobile rendering.
- Run player with `-feast-smoke`; it writes `frame-profile.json` and `player-smoke.txt`. `-feast-mobile-rendering` previews mobile meshes and no-idle-hover behavior on Mac. Android launches may pass these flags through the `unity` intent extra.
- Actual Japanese IME conversion, physical Android device thermal/GPU behavior, and real WebGL browser performance remain unverified. No release configuration, package permissions or production signing was changed.

## Follow-up: emulator 30 FPS target
The final software-GPU rerun after fixing screenshot capture passes with zero runtime errors, 8 successful commands, maximum food 10,110 / visuals 10,132. Median frame 483.33 ms, p95 950 ms; screenshot readback now succeeds and changes the tail workload, so the tail is not directly comparable to the earlier broken-capture runs.

A separately installed official emulator 37.2.13 (original SDK and AVD data preserved), `-gpu host`, MoltenVK, 4 vCPUs and 4 GB RAM reports Apple M3 Pro. The initial Vulkan smoke internally measured 16.67 ms median and p95 with maximum 10,133 food / 10,134 visuals, but the captured frame is **only a blank background** and emulator logs warn `Failed to try unbox VkBuffer 0x1cdc0de`. It is an INVALID graphics result and is **not a 30 FPS success**. `-force-gfx-direct` also remains blank. Emulator version, CPU/RAM and graphics backend changed simultaneously; do not attribute any such result solely to source optimizations.

### Valid hardware-GPU run
On emulator 37.2.13, launching the installed Development APK with `-force-gles -feast-smoke` produces a complete, visually checked game frame (food, table and receipt UI). It reports `Android Emulator OpenGL ES Translator (Apple M3 Pro)`; Unity logs ES 3.0. This is an emulator-specific diagnostic override, NOT a claim that Unity 6.7 supports ES 3.0-only physical phones, and does not alter production graphics settings.

Clean run without a parallel Unity build (UTC 2026-11-18T14:41:03Z): 474 samples, median 16.67 ms, p95/p99 33.33 ms, maximum 100 ms; maximum food 10,133 / visuals 10,135, 9 successful commands, zero runtime errors, View CPU median 1.672 ms. Thus the short smoke workload reaches 60 FPS at median and 30 FPS at p95/p99, but not a guaranteed 30 FPS floor or long-session/physical-device validation. Food instance counts and all effects remain enabled. The major gain over software rendering is the host-GPU path; compare this separately from the same-software-GPU source-only optimization above.

Reproduction: use isolated official emulator 37.2.13, existing API35 ARM64 AVD, `ANDROID_EMU_VK_ICD=moltenvk`, `-no-snapshot-load -no-snapshot-save -gpu host -cores 4 -memory 4096 -feature -VulkanIgnoredHandles,-VulkanDebugUtils`. Launch `local.spinningfeast.receiptuitest/com.unity3d.player.UnityPlayerGameActivity` with string extra `unity` containing `-force-gles -feast-smoke`. The feature toggles did not fix Vulkan rendering and have not yet been independently removed in a GLES control run. For normal interactive play omit `-feast-smoke`.

An experimental non-Development APK was built successfully to investigate Vulkan debug-query behavior, but was not installed or used for the valid measurements above; the optional helper edit was reverted. Installed app remains the measured Development build.

Repeat clean run UTC 2026-11-18T14:41:37Z: 479 samples, maximum food 10,133 / visuals 10,134, 9 successful commands, zero runtime errors. Median/p95/p99/max again 16.67/33.33/33.33/100 ms. Normal interactive title was reopened with `-force-gles` and visually verified. A copy of the installed measured Development APK was preserved separately; the most recent Build output APK is the unused non-Development experiment, so do not confuse them.

## Mobile-GPU budget investigation (not a real-device certification)
ADB currently lists only the emulator. Host Apple GPU frame rates cannot certify Adreno/Mali/PowerVR performance or thermal stability. This pass adds reproducible per-frame submitted food triangles/vertices, draw calls from FeastView, logical matrix payload, and projected shadow-quad area. These counters exclude static scenery, UI and particle draw submissions and do not measure actual driver bandwidth or hidden-surface overdraw. Shadow area includes occluded/off-screen portions, so it is a geometric proxy only.

FrameTimingManager GPU values are recorded only when positive data is available; unsupported data has `gpuTimingAvailable=false`, not a claim of zero GPU cost. Emulator timestamps may not match real GPU execution. Reports include graphics API/device/OS/resolution/mode. Normal Countdown→Playing collection is fixed. `-feast-perf-long` extends the existing deterministic-seed nine-command smoke to the end of the 120-second match; saves stay disabled. `-feast-perf-uncapped` requests 240 FPS for diagnostics, subject to platform refresh limits (it does not guarantee an uncapped display).

Comparison modes in the same APK:
- `-feast-mobile-baseline`: current low-detail mobile meshes, original specular shader path.
- Default mobile: specular-free shader variant on ordinary raw food; golden/cooked food retains specular. No instance-count change.
- `-feast-food-impostors`: experimental, default OFF. Tiny settled non-golden raw food becomes an eight-direction baked cutout quad, still one rendered object per original food. Flying/gold/large/cooked/hovered food stays 3D. This reduces geometry but adds atlas sampling, CPU LOD work and texture memory; it is NOT automatically faster. Initialization does 280 tile renders/readbacks and must be converted to an editor-baked atlas before production use. Pixel/depth/rotation approximation requires visual review.
- Mobile default (or `-feast-small-geometry` on desktop): visually checked small-screen 3D mesh path. `-feast-full-geometry` disables this path for comparison. All ingredient parts/colors/positions retained; spheres, profiles and rods use fewer subdivisions. Gold/cooked/large/hovered food retains original 3D. Larger instancing batches accompany this mode.

Install the development APK on an authorized device, then launch `local.spinningfeast.receiptuitest/com.unity3d.player.UnityPlayerGameActivity` with string intent extra `unity` containing `-feast-smoke -feast-perf-long` plus one comparison mode. On a physical device do NOT use the emulator-only `-force-gles` workaround by default. Pull `files/frame-profile.json`, `files/player-smoke.txt`, and `files/prototype.png` from the app's `/sdcard/Android/data/local.spinningfeast.receiptuitest/` directory after the report timestamp updates. The process exits on test completion. Keep device resolution/thermal state/refresh/API identical across comparisons, repeat and run a 10–15 minute thermal soak before making a shipping performance claim.


### Refined small-screen 3D
The first eight-triangle sphere version reduced work strongly but made large round ingredients too angular, so it was NOT selected. The refined version retains 6-sided/4-row large body spheres and uses 4-sided/2-row spheres only for tiny parts (largest authored semi-axis <= 0.22). It removes only degenerate pole triangles, never ingredient parts. Raw mesh-set triangles are 14,620 -> 5,360, with all 435 types validated and cooked geometry unchanged. Lightweight vertex colors use UNorm8 (validated), reducing the color attribute from 16 to 4 bytes. PC detailed meshes and gold/cooked/large/hovered food remain on original geometry. Contact shadows and particles remain.

Short comparison on the host-GPU emulator at 1080×2220: baseline median food triangles 3,802,586 / draw submissions 120; refined small 3D median 1,373,950 / 82 (about 64% fewer triangles, 32% fewer submitted draws). Maximum food 10,132–10,133, visuals 10,134, 9 successful commands. Refined peak geometry 1,615,588 triangles, 4,846,764 vertices; logical vertex-input work proxy 140,130,480 bytes/frame at peak. This is NOT measured DRAM bandwidth: caching/driver behavior changes actual traffic. Visual inspection retained recognisable ingredients and the dense table, without the first draft's excessive pointed bodies.

The opt-in impostor experiment is retained OFF: its 120-second median geometry fell 5,048,684 -> 2,115,256 triangles, but maximum geometry remained about 6.68M and View CPU rose about 2.16 -> 3.28 ms. Small 3D is preferred for a mobile starting point because it also works during falling/flying food and keeps real 3D depth/rotation. Real-device FPS and thermals remain unverified.

### Current deliverable
The current `Builds/Android/ReceiptUI-dev.apk` is again the final **Development** build, not the earlier unused non-Development experiment. SHA-256: `a27878e84e585719e9d0ab14bf3ed0402dd6f962bf9b71895dc89a55e1fe6d84`. Mobile small 3D and ordinary-food lightweight shading are ON by default; impostors remain OFF. `-feast-mobile-baseline` restores the previous mobile rendering path for in-APK comparison. Food spawning, inventory, cooking rules, animation and input paths were not reduced or capped.

Final default-mobile 120-second test: PASS, zero runtime errors, 9 cooking commands successful, 15,603 food visuals all represented at the end. Median/peak food triangles 1,852,488 / 2,599,628; median/peak submitted draws 75 / 99. Peak logical vertex-input work 258,665,568 bytes, not measured memory bus traffic. Normal full-size dish meshes, golden food, all contact-shadow instances and particles remain. This validates the development build in the emulator, NOT sustained performance on a physical mobile GPU.

Final same-APK 120-second A/B (both maximum food/visuals **15,603**, 9 commands successful):

| Metric | Baseline | Mobile default |
|---|---:|---:|
| Median submitted food triangles | 5,053,796 | 1,852,488 (-63.3%) |
| Peak submitted food triangles | 6,697,264 | 2,599,628 (-61.2%) |
| Median submitted draws (FeastView scope) | 117 | 75 (-35.9%) |
| Peak logical vertex-input bytes | 803,671,680 | 258,665,568 (-67.8%) |
| Peak shadow screen-area proxy | 3.2305 | 3.2305 (unchanged) |
| View CPU median / p95 ms | 2.136 / 2.947 | 2.168 / 3.619 |
| Emulator-reported GPU median ms | 42.10 | 27.74 |
| Frames above 33.5 ms | 4.00% | 1.76% |

Both had frame median/p95/p99 around 16.67/33.33/50 ms; the coarse refresh quantiles alone hide the difference. Do not infer a physical mobile FPS, constant 30 FPS floor, or a CPU speedup from these numbers. The CPU p95 increased, so profiling on the target handset is still required. GPU timings above are emulator-reported host values only. No physical handset is connected and a 10–15 minute thermal soak has not been performed.
