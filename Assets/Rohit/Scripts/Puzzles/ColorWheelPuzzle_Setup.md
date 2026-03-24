# Color Wheel Puzzle Setup (Horror-Style)

## 1) Create Puzzle Root
- Create an empty GameObject in scene: `WallColorWheelPuzzle`.
- Add components:
  - `ColorWheelPuzzleManager`
  - `PuzzleRewardHandler` (optional but recommended)

## 2) Create Wheel Objects
- Create at least 3 child objects under puzzle root:
  - `Dial_A`
  - `Dial_B`
  - `Dial_C`
- Each wheel needs:
  - A visible mesh (with collider)
  - `PuzzleWheel` component
- On each wheel:
  - Set `Rotating Visual` to the mesh transform that should rotate.
  - Set `State Count` (example: 4).
  - Set `Starting State` (example: 0).

## 3) Wire Manager
- On `ColorWheelPuzzleManager`:
  - Click context menu: `Collect Child Wheels`.
  - Set `Target Combination` values (one per wheel).
  - Keep `Lock Wheels When Solved` enabled for a permanent solved feel.
  - Optionally assign:
    - `Solved Indicator Renderer`
    - `Solved Indicator Light`
    - `Solved Sfx`

## 4) Configure Reward
- On `PuzzleRewardHandler`, choose `Reward Mode`:
  - `SpawnPrefab`:
    - Assign `Reward Prefab` (Fuse, Key, or any collectible prefab)
    - Assign `Reward Spawn Point` (optional)
  - `RevealExistingObject`:
    - Assign hidden object in `Existing Reward Object`
  - `ActivateLinkedObject`:
    - Assign lockbox/door/container in `Linked Object To Activate`
- Optional:
  - `Objects To Enable/Disable`
  - `Components To Enable/Disable`
  - `Reward Sfx` / `Reward Vfx`

## 5) Atmosphere Notes (Muted Palette)
- Keep dial materials desaturated:
  - dirty green-grey
  - worn off-white
  - dead brass
  - dark iron
- Avoid high saturation and glossy clean materials.
- Use low-intensity indicator lights for solved state.

## 6) Quick Test
- Enter Play Mode.
- Look at a dial and press its interact key (default `E`) to rotate.
- Match all dial target states.
- Verify:
  - solved triggers once
  - wheels lock (if enabled)
  - reward appears/reveals/activates as configured
