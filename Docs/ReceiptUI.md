# Shared receipt UI Toolkit implementation

Selected direction: **店構えと伝票**. PC and mobile share one runtime controller and exactly four UXML screens (`Title`, `MatchHUD`, `Result`, `Book`). There is no separate Android/portrait screen tree. `Shared.uss` and `Responsive.uss` rearrange the same named nodes using `portrait`, `compact`, and `keyboard-open` classes. No Figma dependency or paid service is used.

## Editing
Open the UXML assets under `Assets/SpinningFeast/Resources/FeastUI` in Unity UI Builder with the Default Runtime Theme. Preserve names queried by `FeastApp.UI.cs`; `ReceiptUITests` checks the contract. Reggae One (SIL OFL 1.1) supplies the noren, display headings, dishes, and stamps. Noto Sans JP remains the body, numeric, and editable-input font. Each checkout ScrollView clones the same ReceiptPaper UXML. Runtime data labels reflect the actual simulation snapshot. UIProposals remain comparison material, not runtime resources.

## Menu and paper system
- Tile and noren motifs, warm red-ruled order slips, a lighter thermal-paper receipt, tear edges, and a decorative barcode are shared across orientations. Decorations do not pick pointer events.
- The player’s successful cook triggers a 460 ms vermilion 受注 stamp. Retrying restarts one stamp rather than queuing visual effects; failures, CPU cooks, and IME composition do not show it. It clears on exiting the playable view.
- The first Result frame starts a 240 ms receipt feed. Page changes do not replay it. Input, paging, replay, and back buttons are not animated. Only opacity and transforms change; no per-frame layout animation, loop, texture generation, or food geometry change.
- `-feast-ui-smoke` checks stamp restart/settle and result feed/settle in addition to navigation. The Android test writes diagnostic captures under its app-specific files path.

- Portrait anchors the tabletop in the upper screen at 95% of safe width. Off-table spill can extend beyond the sides; neither HUD overlays nor IME shrink or shift the table. Android's game activity uses `adjustNothing`; only the input dock follows the measured IME insets (with the resize-aware Unity fallback). Landscape retains its original camera.
- Kitchen calls fire once at 60/30/10 seconds, choose the most urgent cue when a frame skips cutoffs, and clear on result or restart. Checkout has self/opponent tabs with independent positions; each hides its scrollbar and scrolls the full paper by touch, wheel or keyboard.

## Runtime
- Settings and the cookbook pause an active match (including countdown) at its exact remaining time. The ECS, CPU, stock/score and command submissions stop while UI input stays interactive. Timer calls are timed by match time and resume without catch-up.
- Player-facing names are 自分 and 相手, sourced from FeastPlayerNames on title, HUD and checkout. CPU names remain internal.
- `UIDocument` / generated `PanelSettings` own menus, scores, timer, recipe input, cookbook and results.
- Title CPU selection/start/music, gameplay submit/book/music, cookbook close/page/recipe choice, and result replay/title/page are bound to existing game methods.
- Cookbook: 20 entries/page, catalogue order, undiscovered items cannot be selected, vertical scrolling on short screens. Results: a full receipt each for self and opponent, all cooked dishes and per-player totals. Tabs and play/back stay fixed; each tab keeps its own position until the next match.
- Orientation changes do not rebuild nodes or add callbacks. Current input, CPU selection, book page, checkout tab and game state remain intact.
- Safe area is applied once outside the shared screen. Landscape fits a 1280×800 stage; portrait uses a 360-unit-wide stage with the available height. Keyboard handling moves the same input dock rather than creating another UI.
- Native Enter retains composition-string and 80 ms post-composition guards. The submit button uses the same submission path. Result-screen Enter does not replay; F5/Escape retain their original roles. Tab remains available for UI Toolkit focus traversal.
- WebGL retains the native HTML input for Japanese IME. Input bounds and font size come from the live UXML node, not hardcoded coordinates. Browser visual viewport is consulted to keep the dock above a keyboard, and a canvas already resized by its host is not reduced twice. Coarse-pointer devices are not automatically focused, so unrelated taps do not open the software keyboard.
- Existing world labels and transient cooking/countdown effects remain IMGUI. They retain the original 1280×800 presentation-space mapping, with score-flight endpoints mapped to the live UI Toolkit score labels. Only interactive/static UI has migrated. Gameplay/ECS/save systems are unchanged.

## Verification
CLI entry point: `-executeMethod SpinningFeast.Editor.ReceiptUITests.ValidateReceiptUI`.
Checks: all four UXML instantiate; named controls; Japanese font/theme; 320×640, 390×844, 1280×800 layout calculation; notched safe area; cookbook pagination and checkout paper contracts.

`node Tools/test_receipt_web_input.js` performs DOM-mocked bridge checks for composition, post-composition/repeat rejection, responsive font size, no mobile autofocus, viewport and hide/destroy cleanup. It is not a real-browser test.

Verified on macOS Apple M3 Pro with Unity 6000.7.0b9: the four UXML contract tests and mocked WebGL bridge tests pass; gameplay validation passes 1,495 checks, mesh/audio checks pass, and the macOS player builds successfully. Actual player smoke runs at 1280×800 and 390×844 both pass with zero runtime errors and nine successful commands; captured HUD screenshots were visually inspected. These automated smoke runs start in gameplay and do not verify title/result navigation or real Japanese typing. Passing static/CLI tests does **not** certify rendered layout, native Android IME, software keyboards, touchscreen interaction, or WebGL browser behavior. Those require target-device testing. If an Android keyboard reports no rectangle, the controller uses a conservative dock position, which also needs device verification. No Android device is assumed connected and no release/publishing settings are changed.

## Extended Mac and Android validation
- `-feast-ui-smoke` runs an opt-in graphical player test through real Button `NavigationSubmitEvent` routing, with saves/CPU autoplay disabled. It checks initial title focus, CPU selection, start/countdown/gameplay, cookbook paging/selection/close, checkout tab and scroll state, unequal long and empty ledgers, replay and return to title, and writes screenshots plus a scoped report. Fixtures are synthetic; this is not hardware input or IME validation.
- macOS 6000.7.0b9: 1280×800, 390×844 and 320×640 runs pass 13 UI events each, zero runtime errors. The test exposed and fixed a result-ledger invalidation race; result details now refresh when the ECS result phase is observed before the presentation update.
- Normal Mac player: held keyboard input confirmed start, Japanese text paste (`カレー`), Enter submission with scoring, F1 cookbook open/close, natural match completion, Enter not replaying from results, and Escape returning to title. Automated short key taps/pointer coordinates were unreliable; real Japanese IME candidate conversion is still unverified.
- `-buildTarget Android -executeMethod SpinningFeast.Editor.ReceiptAndroidBuild.BuildDevelopmentApk` builds an ARM64/IL2CPP development/debug-key APK under Builds/Android. It uses a temporary test identifier and restores build/signing/identifier settings. Local build passed; no device was connected, so touch, rotation, safe area and software keyboard are not device-tested.
- WebGL player build/browser test could not run because this Mac lacks WebGL Build Support. The bridge DOM-mock tests remain the only WebGL verification.
