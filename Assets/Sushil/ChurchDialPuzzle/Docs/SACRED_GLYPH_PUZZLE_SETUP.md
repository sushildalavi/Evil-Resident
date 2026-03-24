# Sacred Glyph Puzzle Setup

## 1. Architecture Overview

This sequence is a fully in-scene 3D wall puzzle. The player interacts with the painting, the painting reveals the hidden mechanism, the player enters puzzle mode, rotates three physical glyph dials, and the vault opens when the correct step combination is reached.

Mandatory runtime scripts:
- `PaintingFlipReveal`
- `SacredGlyphPuzzleInteractor`
- `SacredGlyphDial` on all 3 dial pivots
- `SacredGlyphPuzzleManager`
- `VaultDoorRevealController`

Optional helpers:
- `PuzzleSolvedReceiver`
- `DialSelectionHighlighter`
- `SacredGlyphPuzzleDebugGizmos`
- `SacredGlyphPuzzleAutoSetupEditor`

Core flow:
1. `PaintingFlipReveal` handles `Press E to Examine`.
2. `PaintingFlipReveal` reveals the panel and optionally auto-enters puzzle mode.
3. `SacredGlyphPuzzleInteractor` owns puzzle-mode input.
4. `SacredGlyphPuzzleManager` checks the 3 current dial steps against the configured solution.
5. `VaultDoorRevealController` opens the hidden chamber when `OnPuzzleSolved` fires.

## 2. Recommended Folder Structure

Use this structure inside the project:

```text
Assets/
├── Scripts/
│   ├── Puzzle/
│   │   ├── SacredGlyphDial.cs
│   │   ├── SacredGlyphPuzzleManager.cs
│   │   ├── SacredGlyphPuzzleInteractor.cs
│   │   ├── PaintingFlipReveal.cs
│   │   ├── VaultDoorRevealController.cs
│   │   ├── PuzzleSolvedReceiver.cs
│   │   ├── SacredGlyphPuzzleDebugGizmos.cs
│   │   └── DialSelectionHighlighter.cs
│   └── Editor/
│       └── SacredGlyphPuzzleAutoSetupEditor.cs
└── Docs/
    └── SACRED_GLYPH_PUZZLE_SETUP.md
```

For this project, the implementation was placed under:

```text
Assets/Sushil/ChurchDialPuzzle/
├── Scripts/
├── Editor/
└── Docs/
```

## 3. Scene Hierarchy

Recommended scene hierarchy:

```text
ChurchPuzzleSequenceRoot
├── PaintingInteractable
├── PaintingVisual
├── PuzzleWallPanel
├── Dial1Pivot
│   └── Dial1Mesh
├── Dial2Pivot
│   └── Dial2Mesh
├── Dial3Pivot
│   └── Dial3Mesh
├── CenterLock
├── InteractionPoint
├── PuzzleManager
├── CameraFocusPoint
├── VaultDoorRoot
│   ├── VaultDoorLeft
│   ├── VaultDoorRight
│   ├── VaultSingleDoor
│   ├── VaultSlidingPanel
│   └── VaultInterior
└── Optional feedback objects
```

Suggested component placement:
- `PaintingInteractable`: `PaintingFlipReveal`
- `Dial1Pivot`, `Dial2Pivot`, `Dial3Pivot`: `SacredGlyphDial`
- `PuzzleManager`: `SacredGlyphPuzzleManager`
- `ChurchPuzzleSequenceRoot`: `SacredGlyphPuzzleInteractor`, optionally `PuzzleSolvedReceiver`, `SacredGlyphPuzzleDebugGizmos`
- `VaultDoorRoot`: `VaultDoorRevealController`

## 4. Inspector Setup Steps

1. Create the hierarchy above.
2. Add `PaintingFlipReveal` to `PaintingInteractable`.
3. Add `SacredGlyphDial` to each dial pivot.
4. Add `SacredGlyphPuzzleManager` to `PuzzleManager`.
5. Add `SacredGlyphPuzzleInteractor` to the root or another central control object.
6. Add `VaultDoorRevealController` to `VaultDoorRoot`.
7. Assign references:
- `PaintingFlipReveal.revealTarget` -> `PaintingVisual`
- `PaintingFlipReveal.puzzleInteractor` -> your `SacredGlyphPuzzleInteractor`
- `SacredGlyphPuzzleInteractor.puzzleManager` -> `PuzzleManager`
- `SacredGlyphPuzzleInteractor.paintingReveal` -> `PaintingInteractable`
- `SacredGlyphPuzzleInteractor.interactionPoint` -> `InteractionPoint`
- `SacredGlyphPuzzleInteractor.cameraFocusPoint` -> `CameraFocusPoint`
- `SacredGlyphPuzzleManager.dial1/dial2/dial3` -> the three dial pivots
- `VaultDoorRevealController` door references -> whichever reveal mode you use
8. In `SacredGlyphPuzzleManager`, configure the three correct solution step values.
9. In `SacredGlyphPuzzleManager.On Puzzle Solved`, add `VaultDoorRevealController.OpenVault`.

## 5. ProBuilder Geometry Setup Notes

Use plain ProBuilder or built-in primitives. No external assets are required.

Recommended geometry:
- `PaintingVisual`: thin rectangular panel
- `PuzzleWallPanel`: recessed wall backing plate
- `Dial1Mesh`, `Dial2Mesh`, `Dial3Mesh`: shallow cylinders or circular plates
- `CenterLock`: decorative center stone or locking piece
- `VaultDoorLeft/Right` or `VaultSingleDoor` or `VaultSlidingPanel`: thick heavy slab geometry

Important geometry rules:
- Each dial mesh should be a child of its pivot.
- Each pivot must sit exactly at the dial rotation center.
- Dials rotate around local Z only.
- Slightly offset the dial meshes in depth to avoid z-fighting with the wall panel.
- If the painting flips open, the reveal target pivot should sit on the hinge side, not at the panel center.

## 6. How to Wire the Painting Reveal

For a hinged painting:
1. Put `PaintingFlipReveal` on `PaintingInteractable`.
2. Set `revealTarget` to `PaintingVisual`.
3. Set `revealMode` to `FlipOpenOnHinge`.
4. Set `openAngle` to something like `90` to `110`.
5. If needed, move the `PaintingVisual` pivot to the hinge edge in ProBuilder or by parenting under a hinge transform.
6. Enable `autoEnterPuzzleAfterReveal` if you want the player to drop directly into puzzle mode.

Other reveal options:
- `RotateAside`: rotate the panel by a custom Euler offset.
- `SlideAside`: slide the panel sideways.
- `DisableVisual`: hide the panel after the reveal.
- `MarkRevealedOnly`: keep the painting visually in place but allow puzzle access.

## 7. How to Wire Puzzle Interaction

Gameplay input while in puzzle mode:
- `1`, `2`, `3` selects Dial 1, Dial 2, Dial 3
- `A` / `Left Arrow` rotates the selected dial counter-clockwise
- `D` / `Right Arrow` rotates the selected dial clockwise
- `Esc` exits puzzle mode

Setup:
1. Make sure `PaintingFlipReveal` references the `SacredGlyphPuzzleInteractor`.
2. Make sure `SacredGlyphPuzzleInteractor` references the `PaintingFlipReveal`.
3. If you want a close-up camera shot, assign `cameraFocusPoint`.
4. If you want the player controller frozen during puzzle mode, let `RohitFPSController.isInPuzzle` handle it, or add extra scripts to `disableWhileInPuzzle`.

## 8. How to Configure Correct Dial Steps

Each dial stores an integer step value from `0` to `totalSteps - 1`.

Example with `totalSteps = 8`:
- Step `0` = first glyph position
- Step `1` = second glyph position
- Step `2` = third glyph position
- Step `7` = final glyph position before wrapping back to `0`

Set the solution here:
- `SacredGlyphPuzzleManager.correctDial1Step`
- `SacredGlyphPuzzleManager.correctDial2Step`
- `SacredGlyphPuzzleManager.correctDial3Step`

The puzzle solves only when:
- Dial 1 current step matches correct step 1
- Dial 2 current step matches correct step 2
- Dial 3 current step matches correct step 3

## 9. How to Wire Puzzle Solved to Vault Opening

Fastest direct wiring:
1. Select the object with `SacredGlyphPuzzleManager`.
2. In `On Puzzle Solved`, add the object with `VaultDoorRevealController`.
3. Choose `VaultDoorRevealController.OpenVault`.

If you want extra scene reactions, add `PuzzleSolvedReceiver` to the root and wire:
- `SacredGlyphPuzzleManager.On Puzzle Solved` -> `PuzzleSolvedReceiver.OnPuzzleSolved`

Then use `PuzzleSolvedReceiver` to:
- enable or disable objects
- enable or disable lights
- trigger animators
- unlock other doors
- forward to `VaultDoorRevealController`

## 10. Fast Path

This is the fastest playable version.

Mandatory:
1. One `PaintingFlipReveal`
2. Three `SacredGlyphDial` components
3. One `SacredGlyphPuzzleManager`
4. One `SacredGlyphPuzzleInteractor`
5. One `VaultDoorRevealController`
6. One `On Puzzle Solved` event wired to `OpenVault`

Optional for later polish:
- camera focus shot
- dial highlighting
- audio
- reveal lights
- extra solved receivers
- debug gizmos

Minimal playable setup:
1. Build the wall, painting, three dials, and one vault door.
2. Put `PaintingFlipReveal` on the front interaction object.
3. Put `SacredGlyphDial` on each dial pivot.
4. Assign all three dials into `SacredGlyphPuzzleManager`.
5. Assign `PaintingFlipReveal` and `SacredGlyphPuzzleManager` into `SacredGlyphPuzzleInteractor`.
6. Wire `SacredGlyphPuzzleManager.On Puzzle Solved` to `VaultDoorRevealController.OpenVault`.
7. Test `E`, `1/2/3`, `A/D`, `Esc`.

## 11. Common Failure Points

Pivots not centered properly:
- The dial will wobble or orbit instead of turning in place.
- Fix by moving the pivot to the exact center of the dial mesh.

Dials rotating around the wrong axis:
- `SacredGlyphDial` rotates around local Z only.
- Fix by rotating the pivot object so the dial face points forward and local Z is the correct spin axis.

Dial steps not wrapping correctly:
- This implementation already wraps `0` backward to the last step and the last step forward to `0`.
- If the dial appears to move the wrong direction, use `invertRotationDirection` on `SacredGlyphDial`.

Puzzle starts already solved:
- Enable `randomizeStartingSteps` and keep `avoidSolvedStart` enabled on `SacredGlyphPuzzleManager`.
- If you do not randomize, manually set non-solved starting steps.

Puzzle reveal and interactor states not synchronized:
- `SacredGlyphPuzzleInteractor` will refuse puzzle mode until `PaintingFlipReveal.IsRevealed` is true.
- Make sure the two scripts reference each other.

Vault reveal triggered multiple times:
- `SacredGlyphPuzzleManager` fires solved once.
- `VaultDoorRevealController.onlyOpenOnce` should stay enabled for normal gameplay.

Inspector references not assigned:
- Missing dial references are the most common reason the solve check never fires.
- Missing `revealTarget` is the most common reason the painting does nothing visually.
- Missing vault door references are the most common reason the solve event appears to work but nothing opens.

Painting reveal not completing before puzzle interaction starts:
- If this happens, disable `autoEnterPuzzleAfterReveal`.
- Let the player press `E` a second time after the reveal completes.

## 12. Test Checklist

Run this checklist in Play Mode:

1. Look at the painting and confirm the prompt appears.
2. Press `E` and verify the painting reveals the hidden mechanism.
3. Confirm the player cannot enter puzzle mode before the reveal finishes.
4. Confirm Dial 1, Dial 2, Dial 3 can be selected with `1`, `2`, `3`.
5. Confirm `A` and `D` rotate only the selected dial.
6. Confirm each dial moves in discrete smooth steps.
7. Confirm the dials stop receiving input while rotating.
8. Confirm an incorrect combination does not trigger the vault.
9. Confirm the correct combination opens the vault exactly once.
10. Confirm `Esc` exits puzzle mode.
11. Confirm re-entering the puzzle after reveal behaves the way you expect.
12. Confirm the vault door motion feels heavy and deliberate.

## 13. Optional Polish Hooks

Good next polish passes:
- Add stone scrape or metallic click sounds on dial turns.
- Add a wood creak or hinge groan on painting reveal.
- Add a dim light inside `VaultInterior` that turns on when the door opens.
- Add a brief solved light pulse with `PuzzleSolvedReceiver`.
- Add `DialSelectionHighlighter` objects for subtle emissive rings or etched glow.
- Add camera focus framing for a more deliberate examination sequence.
- Add a center lock object that reacts when the final combination is correct.
