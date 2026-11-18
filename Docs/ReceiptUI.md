# Shared receipt UI Toolkit implementation

Selected direction: **店構えと伝票**. PC and mobile share one runtime controller and exactly four UXML screens (`Title`, `MatchHUD`, `Result`, `Book`). There is no separate Android/portrait screen tree. `Shared.uss` and `Responsive.uss` rearrange the same named nodes using `portrait`, `compact`, and `keyboard-open` classes. No Figma dependency or paid service is used.

## Editing
Open the UXML assets under `Assets/SpinningFeast/Resources/FeastUI` in Unity UI Builder with the Default Runtime Theme. Preserve names queried by `FeastApp.UI.cs`; `ReceiptUITests` checks the contract. The original Japanese font is reused. Data labels are blank in source assets, populated from the actual simulation at runtime. UIProposals remain comparison material, not runtime resources.

## Runtime
- `UIDocument` / generated `PanelSettings` own menus, scores, timer, recipe input, cookbook and results.
- Title CPU selection/start/music, gameplay submit/book/music, cookbook close/page/recipe choice, and result replay/title/page are bound to existing game methods.
- Cookbook: 20 entries/page, catalogue order, undiscovered items cannot be selected, vertical scrolling on short screens. Results: 6 entries per player/page, wrapping page arrows, empty-state text and actual totals.
- Orientation changes do not rebuild nodes or add callbacks. Current input, CPU selection, book/result page and game state remain intact.
- Safe area is applied once outside the shared screen. Landscape fits a 1280×800 stage; portrait uses a 360-unit-wide stage with the available height. Keyboard handling moves the same input dock rather than creating another UI.
- Native Enter retains composition-string and 80 ms post-composition guards. The submit button uses the same submission path. Result-screen Enter does not replay; F5/Escape retain their original roles. Tab remains available for UI Toolkit focus traversal.
- WebGL retains the native HTML input for Japanese IME. Input bounds and font size come from the live UXML node, not hardcoded coordinates. Browser visual viewport is consulted to keep the dock above a keyboard, and a canvas already resized by its host is not reduced twice. Coarse-pointer devices are not automatically focused, so unrelated taps do not open the software keyboard.
- Existing world labels and transient cooking/countdown effects remain IMGUI. They retain the original 1280×800 presentation-space mapping, with score-flight endpoints mapped to the live UI Toolkit score labels. Only interactive/static UI has migrated. Gameplay/ECS/save systems are unchanged.

## Verification
CLI entry point: `-executeMethod SpinningFeast.Editor.ReceiptUITests.ValidateReceiptUI`.
Checks: all four UXML instantiate; named controls; Japanese font/theme; 320×640, 390×844, 1280×800 layout calculation; notched safe area; cookbook/results pagination coverage.

`node Tools/test_receipt_web_input.js` performs DOM-mocked bridge checks for composition, post-composition/repeat rejection, responsive font size, no mobile autofocus, viewport and hide/destroy cleanup. It is not a real-browser test.

Verified on macOS Apple M3 Pro with Unity 6000.7.0b9: the four UXML contract tests and mocked WebGL bridge tests pass; gameplay validation passes 1,495 checks, mesh/audio checks pass, and the macOS player builds successfully. Actual player smoke runs at 1280×800 and 390×844 both pass with zero runtime errors and nine successful commands; captured HUD screenshots were visually inspected. These automated smoke runs start in gameplay and do not verify title/result navigation or real Japanese typing. Passing static/CLI tests does **not** certify rendered layout, native Android IME, software keyboards, touchscreen interaction, or WebGL browser behavior. Those require target-device testing. If an Android keyboard reports no rectangle, the controller uses a conservative dock position, which also needs device verification. No Android device is assumed connected and no release/publishing settings are changed.
