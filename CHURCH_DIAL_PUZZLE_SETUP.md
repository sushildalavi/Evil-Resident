# Church Dial Puzzle Setup

## Architecture Overview

This system is a world-space mechanical church wall puzzle built from normal scene objects.

- `ChurchDialPuzzleInteractor`
  - World object the player looks at and presses `E` on.
  - Starts the painting reveal and enters puzzle mode.
- `PaintingPuzzleReveal`
  - Handles the front painting/panel behavior.
  - Can swing, slide, fade, disable, or stay in place.
- `ChurchDialPuzzleManager`
  - Owns puzzle mode, dial selection, input, solved check, camera focus, and solved event.
- `ChurchDialPuzzleDial`
  - Lives on each dial pivot.
  - Rotates the pivot around local Z in fixed steps.
- `VaultRevealController`
  - Opens the hidden vault or compartment after solve.
- `PuzzleSolvedReceiver`
  - Generic solved-event receiver for vault, door, lights, sounds, and extra hooks.
- `DialSelectionHighlighter`
  - Optional selected/solved/locked feedback for each dial.
- `ChurchDialPuzzleDebugGizmos`
  - Optional scene gizmos for interaction point, focus point, and dial state.
- `ChurchDialPuzzleAutoSetupEditor`
  - Creates the expected hierarchy, adds components, and wires common references.

## Recommended Folder Structure

Runtime scripts:

- `Assets/Sushil/ChurchDialPuzzle/Scripts/ChurchDialPuzzleDial.cs`
- `Assets/Sushil/ChurchDialPuzzle/Scripts/ChurchDialPuzzleManager.cs`
- `Assets/Sushil/ChurchDialPuzzle/Scripts/ChurchDialPuzzleInteractor.cs`
- `Assets/Sushil/ChurchDialPuzzle/Scripts/PaintingPuzzleReveal.cs`
- `Assets/Sushil/ChurchDialPuzzle/Scripts/VaultRevealController.cs`
- `Assets/Sushil/ChurchDialPuzzle/Scripts/PuzzleSolvedReceiver.cs`
- `Assets/Sushil/ChurchDialPuzzle/Scripts/DialSelectionHighlighter.cs`
- `Assets/Sushil/ChurchDialPuzzle/Scripts/ChurchDialPuzzleDebugGizmos.cs`

Editor tools:

- `Assets/Sushil/ChurchDialPuzzle/Editor/ChurchDialPuzzleAutoSetupEditor.cs`

## Expected Hierarchy

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
│   └── VaultInterior
└── optional feedback objects
```

## Fastest Playable Setup Path

1. Create an empty GameObject and name it `ChurchPuzzleSequenceRoot`.
2. Select it and run `Tools > Church Dial Puzzle > Auto-Setup Selected Root`.
3. The editor tool now generates a usable default stone/brass visual set automatically.
4. If you want to regenerate that default look, run `Tools > Church Dial Puzzle > Rebuild Default Visuals`.
5. Keep each dial mesh child centered at local `0,0,0` under its pivot if you replace the generated meshes.
6. Set the correct step values on `ChurchDialPuzzleManager`.
7. Press Play and test:
   - `E`
   - `1`, `2`, `3`
   - `A`, `D`
   - `Esc`
8. Only after the flow works, replace the generated meshes with nicer ProBuilder geometry if needed.

## Exact Inspector Setup Steps

### 1. Root

- Put the whole sequence under one root object.
- Keep the root transform clean:
  - local rotation `0,0,0`
  - local scale `1,1,1`

### 2. Painting

- `PaintingInteractable`
  - Add `PaintingPuzzleReveal`.
- `PaintingVisual`
  - Put the actual framed art mesh here.
  - If you want reveal colliders disabled after opening, keep those colliders on this object or its children.

Recommended reveal setup:

- `Reveal Mode`
  - `SwingOpen` for a hinged painting
  - `SlideAside` for a sliding panel
  - `FadeOutAndDisable` if you need the fastest result
- `Panel Root`
  - assign `PaintingVisual`

### 3. Dials

- `Dial1Pivot`, `Dial2Pivot`, `Dial3Pivot`
  - Each gets `ChurchDialPuzzleDial`
  - Each gets `DialSelectionHighlighter`
- `Dial1Mesh`, `Dial2Mesh`, `Dial3Mesh`
  - Put the actual ProBuilder dial meshes here

Important pivot rule:

- The pivot object must sit at the exact center of the dial.
- The visible dial mesh child must be centered at local `0,0,0`.
- If the dial art needs slight depth separation, move the mesh depth a small amount only on local Z.
- Do not offset the pivot itself away from the intended rotation center.

### 4. Puzzle Manager

- `PuzzleManager`
  - Add `ChurchDialPuzzleManager`
  - Assign:
    - `Dial1`
    - `Dial2`
    - `Dial3`
    - `Camera Focus Point`
    - optional `Center Lock Renderer`

Recommended defaults:

- `randomizeStartingStepsOnAwake`
  - enabled for gameplay
- `avoidSolvedRandomStart`
  - enabled
- `lockPuzzleAfterSolve`
  - enabled
- `exitPuzzleOnSolve`
  - enabled

### 5. Interaction

- `InteractionPoint`
  - Add `ChurchDialPuzzleInteractor`
  - Ensure it has a `BoxCollider`
  - Keep collider `isTrigger = true`
  - Place it slightly in front of the panel so the player can reliably hit it with the center-screen ray

### 6. Camera Focus

- `CameraFocusPoint`
  - Move this to the position you want the player camera to snap to while solving
  - Rotate it so its forward direction points back toward the puzzle

### 7. Vault

- `VaultDoorRoot`
  - Add `VaultRevealController`
- If using double doors:
  - assign `VaultDoorLeft`
  - assign `VaultDoorRight`
- If using single door:
  - assign `Single Door`
- If using sliding panel:
  - assign `Sliding Panel`
- `VaultInterior`
  - Put hidden reward / compartment geometry / lights here

### 8. Solved Receiver

- On the root object, add `PuzzleSolvedReceiver`
- Assign:
  - optional `Door`
  - optional `Vault Reveal`
  - optional lights, audio, and animators

## How To Wire The Painting To Puzzle Interaction

The normal wiring is:

- `ChurchDialPuzzleInteractor`
  - `puzzleManager` -> `PuzzleManager`
  - `paintingReveal` -> `PaintingInteractable`
  - `interactionPoint` -> `InteractionPoint`

Flow:

- Player looks at `InteractionPoint`
- presses `E`
- `ChurchDialPuzzleInteractor` checks whether painting is already revealed
- if not revealed:
  - `PaintingPuzzleReveal.BeginReveal(...)`
- when reveal completes:
  - it enters puzzle mode through `ChurchDialPuzzleManager.EnterPuzzle(...)`

If you want no painting movement:

- set `PaintingPuzzleReveal` mode to `None`
- keep the camera focus on the dials

## How To Wire Puzzle Solved To Vault Reveal

There are two clean paths.

### Preferred path

- Use the editor tool or wire this manually:
  - `ChurchDialPuzzleManager.onPuzzleSolved`
  - call `PuzzleSolvedReceiver.OnPuzzleSolved`
- In `PuzzleSolvedReceiver`, assign:
  - `vaultReveal` -> `VaultDoorRoot`

### Direct path

- `ChurchDialPuzzleManager.onPuzzleSolved`
  - directly call `VaultRevealController.Reveal`

The receiver path is better because it lets you add door unlocks, light pulses, and sound in one place.

## How To Configure Step Counts And Solution

Each dial has:

- `totalSteps`
- `startingStep`
- `rotateSpeed`

The manager has:

- `correctDial1Step`
- `correctDial2Step`
- `correctDial3Step`

Example:

- 8 steps means each rotation increment is `45` degrees
- A solution of `2,5,1` means:
  - dial 1 must end on step 2
  - dial 2 must end on step 5
  - dial 3 must end on step 1

Useful editor buttons on the manager:

- `Randomize Start Steps`
- `Print Current Step Values`
- `Set Current Steps As Solution`

## How To Test The Full Flow Quickly

1. Set all dial `startingStep` values to `0`.
2. Set manager solution to something simple first, for example `1,2,3`.
3. Disable randomization for the first test.
4. Press Play.
5. Walk up to the interaction point and press `E`.
6. Use `1`, `2`, `3` to switch dials.
7. Use `A` and `D` to rotate.
8. Confirm:
   - only the selected dial rotates
   - input is ignored while that dial is rotating
   - solve happens only when all 3 dials are correct
   - the solved event fires once
   - the vault opens
9. Re-enable random starting steps after the base path works.

## Common Failure Points

- Dial mesh not centered under pivot
  - Result: dial rotates in a wide orbit instead of spinning in place.
- Pivot not at the real center of the dial
  - Result: rotation looks broken.
- Interaction collider sits inside the wall
  - Result: `Press E` is unreliable.
- Camera focus point faces the wrong direction
  - Result: puzzle mode feels broken or shows the back of the panel.
- Painting colliders still block the front after reveal
  - Result: interaction becomes inconsistent.
- Solution values outside dial step count
  - Result: puzzle never solves as expected.
- Randomized start happens to match the solution
  - Result: puzzle starts already solved.
  - Keep `avoidSolvedRandomStart` enabled.
- Extra scale on root or pivots
  - Result: camera movement, rotation speed, and highlight pulse can feel wrong.

## ProBuilder Notes

You do not need procedural geometry generation for this system.

Use ProBuilder or built-in meshes for:

- `PuzzleWallPanel`
- `PaintingVisual`
- `Dial1Mesh`
- `Dial2Mesh`
- `Dial3Mesh`
- `CenterLock`
- `VaultDoorLeft`
- `VaultDoorRight`
- `VaultInterior`

Recommended dial art approach:

- Make each dial a thick disc or ring
- Add simple engraved grooves or raised symbol plates
- Use clear readable shapes
  - crosses
  - triangles
  - circles
  - chevrons
  - sacred runes
- Avoid noisy patterns
- Keep each dial visually distinct so the player can actually reason about the combination

## Optional Polish Ideas

- Add a short metal click sound on every dial step.
- Add a low mechanical hum while in puzzle mode.
- Pulse a candle or spotlight near the puzzle after solve.
- Add a short delay before the vault opens so the solve has weight.
- Add a stone dust particle burst or subtle rumble when the vault reveals.
- Put a reward light inside `VaultInterior` that activates only when open.

## What The Editor Tool Already Does

`Tools > Church Dial Puzzle > Auto-Setup Selected Root` will:

- create the expected hierarchy if pieces are missing
- add the core components
- wire manager, interactor, painting reveal, vault reveal, solved receiver, and gizmos
- add and configure the front interaction `BoxCollider`
- create placeholder mesh child objects for each dial
- wire the solved event to the solved receiver

It does not build your final ProBuilder art for you. That part is still manual, but the required scene wiring is already reduced to a short checklist.
