using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ChurchDialPuzzleDefaultVisualBuilder
{
    private const string PaintingVisualName = "PaintingVisual";
    private const string PuzzleWallPanelName = "PuzzleWallPanel";
    private const string Dial1PivotName = "Dial1Pivot";
    private const string Dial2PivotName = "Dial2Pivot";
    private const string Dial3PivotName = "Dial3Pivot";
    private const string Dial1MeshName = "Dial1Mesh";
    private const string Dial2MeshName = "Dial2Mesh";
    private const string Dial3MeshName = "Dial3Mesh";
    private const string CenterLockName = "CenterLock";
    private const string VaultDoorRootName = "VaultDoorRoot";
    private const string VaultDoorLeftName = "VaultDoorLeft";
    private const string VaultDoorRightName = "VaultDoorRight";
    private const string VaultSingleDoorName = "VaultSingleDoor";
    private const string VaultSlidingPanelName = "VaultSlidingPanel";
    private const string VaultInteriorName = "VaultInterior";
    private const string GeneratedChildName = "GeneratedDefault";
    private const string GeneratedRootFolder = "Assets/Sushil/ChurchDialPuzzle/Generated";
    private const string GeneratedMaterialFolder = GeneratedRootFolder + "/Materials";
    private const string GeneratedTextureFolder = GeneratedRootFolder + "/Textures";

    private struct MaterialSet
    {
        public Material Stone;
        public Material StoneInset;
        public Material Iron;
        public Material Brass;
        public Material BrassDark;
        public Material Wood;
        public Material Parchment;
        public Material PaintingArt;
        public Material PaintedRed;
        public Material AccentBlue;
        public Material AccentGreen;
        public Material AccentCrimson;
        public Material Obsidian;
    }

    public static void RebuildDefaultVisuals(Transform rootCandidate)
    {
        Transform root = ResolveRoot(rootCandidate);
        if (root == null)
            return;

        // Delete and recreate all generated material assets on each rebuild so stale shaders
        // (e.g. from a different render pipeline) never cause the materials to appear black.
        PurgeMaterialAssets();
        MaterialSet materials = GetMaterialSet();

        Transform paintingVisual = FindOrCreateChild(root, PaintingVisualName);
        Transform panel = FindOrCreateChild(root, PuzzleWallPanelName);
        Transform dial1Pivot = FindOrCreateChild(root, Dial1PivotName);
        Transform dial2Pivot = FindOrCreateChild(root, Dial2PivotName);
        Transform dial3Pivot = FindOrCreateChild(root, Dial3PivotName);
        Transform centerLock = FindOrCreateChild(root, CenterLockName);
        Transform vaultRoot = FindOrCreateChild(root, VaultDoorRootName);
        Transform vaultLeft = FindOrCreateChild(vaultRoot, VaultDoorLeftName);
        Transform vaultRight = FindOrCreateChild(vaultRoot, VaultDoorRightName);
        Transform vaultSingleDoor = FindOrCreateChild(vaultRoot, VaultSingleDoorName);
        Transform vaultSlidingPanel = FindOrCreateChild(vaultRoot, VaultSlidingPanelName);
        Transform vaultInterior = FindOrCreateChild(vaultRoot, VaultInteriorName);

        Transform dial1Mesh = FindOrCreateChild(dial1Pivot, Dial1MeshName);
        Transform dial2Mesh = FindOrCreateChild(dial2Pivot, Dial2MeshName);
        Transform dial3Mesh = FindOrCreateChild(dial3Pivot, Dial3MeshName);

        BuildPaintingVisual(paintingVisual, materials);
        BuildWallPanel(panel, dial1Pivot.localPosition, dial2Pivot.localPosition, dial3Pivot.localPosition, materials);
        BuildDial(dial1Mesh, 0, materials);
        BuildDial(dial2Mesh, 1, materials);
        BuildDial(dial3Mesh, 2, materials);
        BuildCenterLock(centerLock, materials);
        BuildVault(vaultLeft, vaultRight, vaultSingleDoor, vaultSlidingPanel, vaultInterior, materials);

        EditorUtility.SetDirty(root.gameObject);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }

    private static void BuildPaintingVisual(Transform paintingVisual, MaterialSet materials)
    {
        Transform generated = RebuildGeneratedChild(paintingVisual);

        // Backing slab — dark ebony wood
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "FrameBack",   Vector3.zero,              Vector3.zero, new Vector3(1.6f,  2.2f,  0.07f), materials.Wood);
        // Inner gold border liner
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "FrameLiner",  new Vector3(0f, 0f, 0.022f), Vector3.zero, new Vector3(1.44f, 2.04f, 0.030f), materials.Brass);
        // Deep obsidian background of the canvas
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "CanvasInset", new Vector3(0f, 0f, 0.030f), Vector3.zero, new Vector3(1.24f, 1.84f, 0.020f), materials.Obsidian);
        // Painted surface — Eye of Horus texture
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "CanvasArt",   new Vector3(0f, 0f, 0.046f), Vector3.zero, new Vector3(1.16f, 1.74f, 0.008f), materials.PaintingArt);

        // Gold outer cartouche molding — top and bottom are heavier
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "TopMolding",    new Vector3(0f,  1.05f, 0.042f), Vector3.zero, new Vector3(1.54f, 0.14f, 0.046f), materials.Brass);
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "BottomMolding", new Vector3(0f, -1.05f, 0.042f), Vector3.zero, new Vector3(1.54f, 0.14f, 0.046f), materials.Brass);
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "LeftMolding",   new Vector3(-0.74f, 0f, 0.040f), Vector3.zero, new Vector3(0.14f, 1.94f, 0.042f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "RightMolding",  new Vector3( 0.74f, 0f, 0.040f), Vector3.zero, new Vector3(0.14f, 1.94f, 0.042f), materials.BrassDark);

        // Corner rosette bosses (Egyptian lotus corners)
        float[] cx = { -0.74f, 0.74f, -0.74f, 0.74f };
        float[] cy = {  1.05f, 1.05f, -1.05f, -1.05f };
        for (int i = 0; i < 4; i++)
            CreatePrimitiveChild(generated, PrimitiveType.Cylinder, $"CornerBoss_{i}",
                new Vector3(cx[i], cy[i], 0.060f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.018f, 0.12f), materials.Brass);

        // Crest — winged sun disk (simplified: sun disk + two wing bars)
        Transform crest = CreateEmptyChild(generated, "Crest");
        crest.localPosition = new Vector3(0f, 1.18f, 0.062f);
        CreatePrimitiveChild(crest, PrimitiveType.Cylinder, "SunDisk",   Vector3.zero,              new Vector3(90f,0f,0f), new Vector3(0.20f, 0.022f, 0.20f), materials.Brass);
        CreatePrimitiveChild(crest, PrimitiveType.Cylinder, "SunCore",   new Vector3(0f, 0f, 0.012f), new Vector3(90f,0f,0f), new Vector3(0.08f, 0.014f, 0.08f), materials.BrassDark);
        CreatePrimitiveChild(crest, PrimitiveType.Cube,     "WingLeft",  new Vector3(-0.26f, 0f, 0.008f), new Vector3(0f, 0f, 8f),  new Vector3(0.28f, 0.06f, 0.018f), materials.BrassDark);
        CreatePrimitiveChild(crest, PrimitiveType.Cube,     "WingRight", new Vector3( 0.26f, 0f, 0.008f), new Vector3(0f, 0f, -8f), new Vector3(0.28f, 0.06f, 0.018f), materials.BrassDark);

        // Bottom scarab seals (lapis, gold, turquoise)
        Material[] sealColors = { materials.AccentBlue, materials.Brass, materials.AccentGreen };
        for (int i = 0; i < 3; i++)
        {
            float x = -0.44f + (i * 0.44f);
            CreatePrimitiveChild(generated, PrimitiveType.Cylinder, $"Seal_{i}",
                new Vector3(x, -0.94f, 0.052f), new Vector3(90f, 0f, 0f), new Vector3(0.072f, 0.014f, 0.072f), sealColors[i]);
        }
    }

    private static void BuildWallPanel(Transform panel, Vector3 dial1Position, Vector3 dial2Position, Vector3 dial3Position, MaterialSet materials)
    {
        Transform generated = RebuildGeneratedChild(panel);

        // Main sandstone slab
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "StoneBack",   Vector3.zero,                     Vector3.zero, new Vector3(2.46f, 3.46f, 0.24f), materials.Stone);
        // Inset carved panel (shadowed recess)
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "InsetPlate",  new Vector3(0f, 0f, 0.065f),     Vector3.zero, new Vector3(1.94f, 2.82f, 0.06f), materials.StoneInset);
        // Gold-capped lintel top
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "TopCap",      new Vector3(0f,  1.84f, 0.045f), Vector3.zero, new Vector3(1.98f, 0.30f, 0.10f), materials.BrassDark);
        // Gold base plinth
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "BaseCap",     new Vector3(0f, -1.84f, 0.045f), Vector3.zero, new Vector3(1.98f, 0.30f, 0.10f), materials.BrassDark);

        // Egyptian-style engaged columns (wider with slight batter)
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "LeftColumn",  new Vector3(-1.06f, 0f, 0.030f), Vector3.zero, new Vector3(0.22f, 3.08f, 0.14f), materials.Stone);
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "RightColumn", new Vector3( 1.06f, 0f, 0.030f), Vector3.zero, new Vector3(0.22f, 3.08f, 0.14f), materials.Stone);
        // Column gold caps (lotus-capital hint)
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "LeftColCap",  new Vector3(-1.06f,  1.60f, 0.06f), new Vector3(90f,0f,0f), new Vector3(0.22f, 0.012f, 0.22f), materials.Brass);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "RightColCap", new Vector3( 1.06f,  1.60f, 0.06f), new Vector3(90f,0f,0f), new Vector3(0.22f, 0.012f, 0.22f), materials.Brass);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "LeftColBase", new Vector3(-1.06f, -1.60f, 0.06f), new Vector3(90f,0f,0f), new Vector3(0.22f, 0.012f, 0.22f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "RightColBase",new Vector3( 1.06f, -1.60f, 0.06f), new Vector3(90f,0f,0f), new Vector3(0.22f, 0.012f, 0.22f), materials.BrassDark);

        // Sun disk (Aten/Ra) above the dial cluster
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "SunDisk",  new Vector3(0f, 1.10f, 0.095f), new Vector3(90f,0f,0f), new Vector3(0.30f, 0.022f, 0.30f), materials.Brass);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "SunRing",  new Vector3(0f, 1.10f, 0.115f), new Vector3(90f,0f,0f), new Vector3(0.22f, 0.014f, 0.22f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "SunCore",  new Vector3(0f, 1.10f, 0.125f), new Vector3(90f,0f,0f), new Vector3(0.10f, 0.014f, 0.10f), materials.Brass);
        // Sun-disk cardinal rays
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "SunRayN", new Vector3(0f, 1.26f, 0.095f),  Vector3.zero, new Vector3(0.04f, 0.12f, 0.015f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "SunRayS", new Vector3(0f, 0.94f, 0.095f),  Vector3.zero, new Vector3(0.04f, 0.12f, 0.015f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "SunRayE", new Vector3(0.16f, 1.10f, 0.095f), Vector3.zero, new Vector3(0.12f, 0.04f, 0.015f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "SunRayW", new Vector3(-0.16f, 1.10f, 0.095f), Vector3.zero, new Vector3(0.12f, 0.04f, 0.015f), materials.BrassDark);

        // Hieroglyph band — carved inscription strip below sun disk
        CreatePrimitiveChild(generated, PrimitiveType.Cube, "GlyphBand", new Vector3(0f, 0.80f, 0.075f), Vector3.zero, new Vector3(1.60f, 0.09f, 0.02f), materials.BrassDark);
        // Individual glyph marks (4 evenly spaced, turquoise inlay hint)
        for (int gi = 0; gi < 4; gi++)
        {
            float gx = -0.54f + gi * 0.36f;
            CreatePrimitiveChild(generated, PrimitiveType.Cube, $"GlyphMark_{gi}", new Vector3(gx, 0.80f, 0.088f), Vector3.zero, new Vector3(0.06f, 0.060f, 0.014f), materials.AccentGreen);
        }

        // Dial recesses and pointers
        CreateDialRecess(generated, "Dial1Recess", dial1Position, materials);
        CreateDialRecess(generated, "Dial2Recess", dial2Position, materials);
        CreateDialRecess(generated, "Dial3Recess", dial3Position, materials);

        CreatePointer(generated, "Dial1Pointer", dial1Position + new Vector3(0f, 0.42f, 0.08f), materials.Brass);
        CreatePointer(generated, "Dial2Pointer", dial2Position + new Vector3(0f, 0.42f, 0.08f), materials.Brass);
        CreatePointer(generated, "Dial3Pointer", dial3Position + new Vector3(0f, 0.42f, 0.08f), materials.Brass);

        // Hanging chain links connecting dial 1&3 down to the center lock area
        CreateChainLinkRun(generated, new Vector3(-0.5f, -0.22f, 0.095f), new Vector3(0f, -0.58f, 0.09f), 5, materials.BrassDark);
        CreateChainLinkRun(generated, new Vector3( 0.5f, -0.22f, 0.095f), new Vector3(0f, -0.58f, 0.09f), 5, materials.BrassDark);
    }

    private static void BuildDial(Transform meshRoot, int dialIndex, MaterialSet materials)
    {
        meshRoot.localPosition = Vector3.zero;
        meshRoot.localRotation = Quaternion.identity;
        meshRoot.localScale = Vector3.one;

        Transform generated = RebuildGeneratedChild(meshRoot);
        Material accent = GetAccentMaterial(dialIndex, materials);

        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "ShadowPlate", new Vector3(0f, 0f, -0.01f), new Vector3(90f, 0f, 0f), new Vector3(0.84f, 0.02f, 0.84f), materials.Obsidian);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "OuterRim", Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.8f, 0.03f, 0.8f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "SecondaryRim", new Vector3(0f, 0f, 0.008f), new Vector3(90f, 0f, 0f), new Vector3(0.73f, 0.015f, 0.73f), materials.Brass);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "MainPlate", new Vector3(0f, 0f, 0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.66f, 0.035f, 0.66f), materials.Iron);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "InnerPlate", new Vector3(0f, 0f, 0.044f), new Vector3(90f, 0f, 0f), new Vector3(0.52f, 0.014f, 0.52f), materials.StoneInset);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "CenterBoss", new Vector3(0f, 0f, 0.064f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 0.02f, 0.22f), materials.Brass);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 studPosition = AngleToPoint(angle, 0.46f, 0.062f);
            CreatePrimitiveChild(generated, PrimitiveType.Sphere, $"Stud_{i}", studPosition, Vector3.zero, Vector3.one * 0.05f, i == 0 ? accent : materials.BrassDark);
        }

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            bool accentMark = i == 0;
            CreateDialTick(generated, $"Tick_{i}", angle, accentMark ? accent : materials.Brass, accentMark ? 0.18f : 0.11f, accentMark ? 0.05f : 0.03f, 0.31f, 0.078f);
            CreateDialRune(generated, $"Rune_{i}", angle, dialIndex, i, accentMark ? accent : materials.BrassDark);
        }

        BuildCenterSigil(generated, dialIndex, accent, materials.BrassDark);
    }

    // Egyptian cartouche lock — oval plaque with hieroglyphic inscription and Ankh-key slot.
    private static void BuildCenterLock(Transform centerLock, MaterialSet materials)
    {
        Transform generated = RebuildGeneratedChild(centerLock);

        // Outer gold cartouche oval
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "Cartouche",  Vector3.zero,              new Vector3(90f,0f,0f), new Vector3(0.50f, 0.032f, 0.28f), materials.Brass);
        // Inner dark inset (obsidian inlay)
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "Inlay",      new Vector3(0f, 0f, 0.020f), new Vector3(90f,0f,0f), new Vector3(0.38f, 0.014f, 0.18f), materials.Obsidian);
        // Tie-bar at bottom of cartouche
        CreatePrimitiveChild(generated, PrimitiveType.Cube,     "TieBar",     new Vector3(0f, -0.155f, 0.012f), Vector3.zero,     new Vector3(0.46f, 0.042f, 0.028f), materials.Brass);

        // Ankh-key slot (vertical stem + round head)
        CreatePrimitiveChild(generated, PrimitiveType.Cube,     "AnkhSlotStem", new Vector3(0f, -0.030f, 0.028f), Vector3.zero,       new Vector3(0.032f, 0.110f, 0.020f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cylinder, "AnkhSlotHead", new Vector3(0f,  0.050f, 0.028f), new Vector3(90f,0f,0f), new Vector3(0.058f, 0.016f, 0.058f), materials.BrassDark);
        CreatePrimitiveChild(generated, PrimitiveType.Cube,     "AnkhCross",    new Vector3(0f,  0.012f, 0.032f), Vector3.zero,       new Vector3(0.068f, 0.018f, 0.014f), materials.BrassDark);

        // Lapis inlay cartouche marks (three symbolic glyphs)
        float[] lapX = { -0.10f, 0f, 0.10f };
        for (int i = 0; i < 3; i++)
            CreatePrimitiveChild(generated, PrimitiveType.Cube, $"Glyph_{i}",
                new Vector3(lapX[i], 0.02f, 0.030f), Vector3.zero, new Vector3(0.030f, 0.060f, 0.012f),
                i == 1 ? materials.AccentCrimson : materials.AccentBlue);
    }

    private static void BuildVault(Transform vaultLeft, Transform vaultRight, Transform vaultSingleDoor, Transform vaultSlidingPanel, Transform vaultInterior, MaterialSet materials)
    {
        Transform leftGenerated = RebuildGeneratedChild(vaultLeft);
        Transform rightGenerated = RebuildGeneratedChild(vaultRight);
        Transform singleGenerated = RebuildGeneratedChild(vaultSingleDoor);
        Transform slidingGenerated = RebuildGeneratedChild(vaultSlidingPanel);
        Transform interiorGenerated = RebuildGeneratedChild(vaultInterior);

        // LEFT door leaf — sandstone slab with gold banding and cartouche motif
        CreatePrimitiveChild(leftGenerated, PrimitiveType.Cube,     "Leaf",       new Vector3(0.28f, 0f, 0f),       Vector3.zero,           new Vector3(0.56f, 1.50f, 0.10f), materials.Stone);
        CreatePrimitiveChild(leftGenerated, PrimitiveType.Cube,     "GoldBandTop",new Vector3(0.28f,  0.66f, 0.054f), Vector3.zero,           new Vector3(0.50f, 0.06f, 0.020f), materials.Brass);
        CreatePrimitiveChild(leftGenerated, PrimitiveType.Cube,     "GoldBandBot",new Vector3(0.28f, -0.66f, 0.054f), Vector3.zero,          new Vector3(0.50f, 0.06f, 0.020f), materials.Brass);
        CreatePrimitiveChild(leftGenerated, PrimitiveType.Cylinder, "CartoucheL", new Vector3(0.28f, 0f, 0.058f),   new Vector3(90f,0f,0f), new Vector3(0.22f, 0.014f, 0.36f), materials.BrassDark);
        CreatePrimitiveChild(leftGenerated, PrimitiveType.Cube,     "EyeMarkL",   new Vector3(0.28f, 0.04f, 0.065f), Vector3.zero,           new Vector3(0.14f, 0.028f, 0.012f), materials.AccentBlue);

        // RIGHT door leaf — mirrored
        CreatePrimitiveChild(rightGenerated, PrimitiveType.Cube,     "Leaf",       new Vector3(-0.28f, 0f, 0f),       Vector3.zero,           new Vector3(0.56f, 1.50f, 0.10f), materials.Stone);
        CreatePrimitiveChild(rightGenerated, PrimitiveType.Cube,     "GoldBandTop",new Vector3(-0.28f,  0.66f, 0.054f), Vector3.zero,          new Vector3(0.50f, 0.06f, 0.020f), materials.Brass);
        CreatePrimitiveChild(rightGenerated, PrimitiveType.Cube,     "GoldBandBot",new Vector3(-0.28f, -0.66f, 0.054f), Vector3.zero,          new Vector3(0.50f, 0.06f, 0.020f), materials.Brass);
        CreatePrimitiveChild(rightGenerated, PrimitiveType.Cylinder, "CartoucheR", new Vector3(-0.28f, 0f, 0.058f),   new Vector3(90f,0f,0f), new Vector3(0.22f, 0.014f, 0.36f), materials.BrassDark);
        CreatePrimitiveChild(rightGenerated, PrimitiveType.Cube,     "EyeMarkR",   new Vector3(-0.28f, 0.04f, 0.065f), Vector3.zero,           new Vector3(0.14f, 0.028f, 0.012f), materials.AccentBlue);

        // SINGLE sealed-door slab (heavy tomb-door look)
        CreatePrimitiveChild(singleGenerated, PrimitiveType.Cube,     "Slab",     Vector3.zero,              Vector3.zero,           new Vector3(1.12f, 1.50f, 0.10f), materials.Stone);
        CreatePrimitiveChild(singleGenerated, PrimitiveType.Cube,     "BandTop",  new Vector3(0f,  0.66f, 0.056f), Vector3.zero,     new Vector3(1.00f, 0.06f, 0.020f), materials.Brass);
        CreatePrimitiveChild(singleGenerated, PrimitiveType.Cube,     "BandBot",  new Vector3(0f, -0.66f, 0.056f), Vector3.zero,     new Vector3(1.00f, 0.06f, 0.020f), materials.Brass);
        CreatePrimitiveChild(singleGenerated, PrimitiveType.Cylinder, "SunDisk",  new Vector3(0f, 0.28f, 0.062f), new Vector3(90f,0f,0f), new Vector3(0.20f, 0.016f, 0.20f), materials.Brass);
        CreatePrimitiveChild(singleGenerated, PrimitiveType.Cylinder, "SunCore",  new Vector3(0f, 0.28f, 0.072f), new Vector3(90f,0f,0f), new Vector3(0.08f, 0.014f, 0.08f), materials.BrassDark);
        CreatePrimitiveChild(singleGenerated, PrimitiveType.Cylinder, "WheelLock",new Vector3(0f, 0f,   0.065f), new Vector3(90f,0f,0f), new Vector3(0.16f, 0.030f, 0.16f), materials.Brass);

        // Sliding panel — sandstone slab with sun disk sigil
        CreatePrimitiveChild(slidingGenerated, PrimitiveType.Cube,     "Slab",    Vector3.zero,                Vector3.zero,           new Vector3(1.18f, 1.58f, 0.12f), materials.Stone);
        CreatePrimitiveChild(slidingGenerated, PrimitiveType.Cube,     "Inset",   new Vector3(0f, 0f, 0.070f), Vector3.zero,           new Vector3(0.94f, 1.32f, 0.022f), materials.StoneInset);
        CreatePrimitiveChild(slidingGenerated, PrimitiveType.Cylinder, "SunDisk", new Vector3(0f, 0f, 0.092f), new Vector3(90f,0f,0f), new Vector3(0.20f, 0.018f, 0.20f), materials.Brass);
        CreatePrimitiveChild(slidingGenerated, PrimitiveType.Cylinder, "SunCore", new Vector3(0f, 0f, 0.104f), new Vector3(90f,0f,0f), new Vector3(0.08f, 0.014f, 0.08f), materials.BrassDark);

        // Tomb interior — sandstone walls, stone shelf, gold relic stand
        CreatePrimitiveChild(interiorGenerated, PrimitiveType.Cube,     "BackWall",   new Vector3(0f, 0f, -0.45f),   Vector3.zero,           new Vector3(1.18f, 1.72f, 0.08f), materials.Stone);
        CreatePrimitiveChild(interiorGenerated, PrimitiveType.Cube,     "LeftWall",   new Vector3(-0.55f, 0f, -0.22f), Vector3.zero,          new Vector3(0.08f, 1.72f, 0.46f), materials.StoneInset);
        CreatePrimitiveChild(interiorGenerated, PrimitiveType.Cube,     "RightWall",  new Vector3( 0.55f, 0f, -0.22f), Vector3.zero,          new Vector3(0.08f, 1.72f, 0.46f), materials.StoneInset);
        CreatePrimitiveChild(interiorGenerated, PrimitiveType.Cube,     "Shelf",      new Vector3(0f, -0.48f, -0.1f), Vector3.zero,           new Vector3(0.90f, 0.10f, 0.46f), materials.Stone);
        // Gold relic stand
        CreatePrimitiveChild(interiorGenerated, PrimitiveType.Cylinder, "RelicBase",  new Vector3(0f, -0.22f, -0.02f), new Vector3(90f,0f,0f), new Vector3(0.20f, 0.040f, 0.20f), materials.Brass);
        CreatePrimitiveChild(interiorGenerated, PrimitiveType.Cylinder, "RelicPedestal", new Vector3(0f, 0.02f, -0.02f), new Vector3(90f,0f,0f), new Vector3(0.10f, 0.010f, 0.10f), materials.BrassDark);
        // Lapis glowing relic halo
        CreatePrimitiveChild(interiorGenerated, PrimitiveType.Cylinder, "RelicHalo",  new Vector3(0f, 0.10f,  0.02f), new Vector3(90f,0f,0f), new Vector3(0.14f, 0.012f, 0.14f), materials.AccentBlue);
        // Back wall painting panel (Egyptian scene)
        CreatePrimitiveChild(interiorGenerated, PrimitiveType.Cube,     "InnerPanel", new Vector3(0f, 0.10f, -0.40f), Vector3.zero,           new Vector3(0.82f, 0.94f, 0.010f), materials.PaintingArt);
    }

    private static void CreateDialRecess(Transform parent, string name, Vector3 localPosition, MaterialSet materials)
    {
        CreatePrimitiveChild(parent, PrimitiveType.Cylinder, name + "_Plate", localPosition + new Vector3(0f, 0f, 0.01f), new Vector3(90f, 0f, 0f), new Vector3(0.9f, 0.05f, 0.9f), materials.BrassDark);
        CreatePrimitiveChild(parent, PrimitiveType.Cylinder, name + "_Shadow", localPosition + new Vector3(0f, 0f, -0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.74f, 0.03f, 0.74f), materials.StoneInset);
    }

    private static void CreatePointer(Transform parent, string name, Vector3 localPosition, Material material)
    {
        Transform pointer = CreateEmptyChild(parent, name);
        pointer.localPosition = localPosition;
        pointer.localRotation = Quaternion.identity;
        pointer.localScale = Vector3.one;

        CreatePrimitiveChild(pointer, PrimitiveType.Cube, "Stem", Vector3.zero, Vector3.zero, new Vector3(0.06f, 0.14f, 0.03f), material);
        CreatePrimitiveChild(pointer, PrimitiveType.Cube, "Tip", new Vector3(0f, -0.08f, 0.005f), new Vector3(0f, 0f, 45f), new Vector3(0.08f, 0.08f, 0.02f), material);
    }

    private static void CreateChainLinkRun(Transform parent, Vector3 start, Vector3 end, int links, Material material)
    {
        if (links < 2)
            return;

        for (int i = 0; i < links; i++)
        {
            float t = links == 1 ? 0f : i / (float)(links - 1);
            Vector3 position = Vector3.Lerp(start, end, t);
            float zRotation = i % 2 == 0 ? 0f : 90f;
            CreatePrimitiveChild(parent, PrimitiveType.Cylinder, $"ChainLink_{i}", position, new Vector3(90f, 0f, zRotation), new Vector3(0.04f, 0.01f, 0.06f), material);
        }
    }

    private static void CreateDialTick(Transform parent, string name, float angleDegrees, Material material, float length, float width, float radius, float z)
    {
        Vector3 position = AngleToPoint(angleDegrees, radius, z);
        CreatePrimitiveChild(parent, PrimitiveType.Cube, name, position, new Vector3(0f, 0f, angleDegrees), new Vector3(width, length, 0.025f), material);
    }

    // Egyptian hieroglyph-style rune motifs etched onto each dial position.
    //   motif 0 = Ankh (cross + oval loop)
    //   motif 1 = Eye of Horus (horizontal ellipse + pupil + outer tail)
    //   motif 2 = Scarab (oval body + pair of foreleg lines)
    //   motif 3 = Djed Pillar (stacked horizontal bars tapering upward)
    private static void CreateDialRune(Transform parent, string name, float angleDegrees, int dialIndex, int stepIndex, Material material)
    {
        Transform rune = CreateEmptyChild(parent, name);
        rune.localPosition = AngleToPoint(angleDegrees, 0.2f, 0.075f);
        rune.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
        rune.localScale = Vector3.one;

        int motif = (dialIndex + stepIndex) % 4;
        switch (motif)
        {
            case 0: // Ankh — vertical stem + crossbar + oval loop
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Stem",     new Vector3(0f, -0.026f, 0f), Vector3.zero,           new Vector3(0.020f, 0.070f, 0.018f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Cross",    new Vector3(0f,  0.016f, 0f), Vector3.zero,           new Vector3(0.072f, 0.018f, 0.018f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cylinder, "Loop",     new Vector3(0f,  0.055f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.034f, 0.008f, 0.034f), material);
                break;

            case 1: // Eye of Horus — almond outline + vertical pupil + outer kohl tail
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "EyeH",     Vector3.zero,               Vector3.zero,           new Vector3(0.082f, 0.022f, 0.018f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cylinder, "Pupil",    new Vector3(0f, 0f, 0.008f), new Vector3(90f, 0f, 0f), new Vector3(0.014f, 0.008f, 0.022f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Tail",     new Vector3(0.054f, 0f, 0f), Vector3.zero,           new Vector3(0.024f, 0.010f, 0.016f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Drop",     new Vector3(-0.044f, -0.026f, 0f), Vector3.zero,     new Vector3(0.010f, 0.022f, 0.016f), material);
                break;

            case 2: // Scarab beetle — oval body + two bent foreleg lines
                CreatePrimitiveChild(rune, PrimitiveType.Cylinder, "Body",     Vector3.zero,               new Vector3(90f, 0f, 0f), new Vector3(0.040f, 0.008f, 0.056f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "LegA",     new Vector3(-0.028f,  0.014f, 0f), new Vector3(0f, 0f,  40f), new Vector3(0.012f, 0.050f, 0.016f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "LegB",     new Vector3( 0.028f,  0.014f, 0f), new Vector3(0f, 0f, -40f), new Vector3(0.012f, 0.050f, 0.016f), material);
                break;

            default: // Djed Pillar — four stacked horizontal bars, widening towards base
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Bar1",     new Vector3(0f,  0.050f, 0f), Vector3.zero, new Vector3(0.050f, 0.014f, 0.018f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Bar2",     new Vector3(0f,  0.022f, 0f), Vector3.zero, new Vector3(0.062f, 0.014f, 0.018f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Bar3",     new Vector3(0f, -0.008f, 0f), Vector3.zero, new Vector3(0.074f, 0.014f, 0.018f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Bar4",     new Vector3(0f, -0.038f, 0f), Vector3.zero, new Vector3(0.088f, 0.018f, 0.018f), material);
                CreatePrimitiveChild(rune, PrimitiveType.Cube,     "Spine",    new Vector3(0f, -0.010f, 0f), Vector3.zero, new Vector3(0.016f, 0.110f, 0.016f), material);
                break;
        }
    }

    // Centre sigil on each dial — Egyptian sacred symbols:
    //   Dial 0 = Ankh (life), Dial 1 = Eye of Horus (protection), Dial 2 = Sun Disk (Ra)
    private static void BuildCenterSigil(Transform parent, int dialIndex, Material accent, Material backingMaterial)
    {
        Transform sigil = CreateEmptyChild(parent, "CenterSigil");
        sigil.localPosition = new Vector3(0f, 0f, 0.08f);
        sigil.localRotation = Quaternion.identity;
        sigil.localScale = Vector3.one;

        // Dark backing cartouche
        CreatePrimitiveChild(sigil, PrimitiveType.Cylinder, "Backer", Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.13f, 0.008f, 0.13f), backingMaterial);

        switch (dialIndex)
        {
            case 0: // Ankh — symbol of eternal life
                CreatePrimitiveChild(sigil, PrimitiveType.Cube,     "Stem",  new Vector3(0f, -0.030f, 0f), Vector3.zero,           new Vector3(0.022f, 0.080f, 0.018f), accent);
                CreatePrimitiveChild(sigil, PrimitiveType.Cube,     "Cross", new Vector3(0f,  0.010f, 0f), Vector3.zero,           new Vector3(0.095f, 0.020f, 0.018f), accent);
                CreatePrimitiveChild(sigil, PrimitiveType.Cylinder, "Loop",  new Vector3(0f,  0.060f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.040f, 0.010f, 0.040f), accent);
                break;

            case 1: // Wedjat — Eye of Horus: almond + pupil + outer kohl tail
                CreatePrimitiveChild(sigil, PrimitiveType.Cube,     "Almond",  Vector3.zero,               Vector3.zero,           new Vector3(0.100f, 0.024f, 0.018f), accent);
                CreatePrimitiveChild(sigil, PrimitiveType.Cylinder, "Pupil",   new Vector3(0f, 0f, 0.010f), new Vector3(90f, 0f, 0f), new Vector3(0.018f, 0.009f, 0.026f), accent);
                CreatePrimitiveChild(sigil, PrimitiveType.Cube,     "Tail",    new Vector3(0.064f, -0.016f, 0f), Vector3.zero,     new Vector3(0.028f, 0.010f, 0.016f), accent);
                break;

            default: // Aten / Ra — sun disk with inner circle and ring
                CreatePrimitiveChild(sigil, PrimitiveType.Cylinder, "Disk",    Vector3.zero,               new Vector3(90f, 0f, 0f), new Vector3(0.082f, 0.010f, 0.082f), accent);
                CreatePrimitiveChild(sigil, PrimitiveType.Cylinder, "Ring",    new Vector3(0f, 0f, 0.006f), new Vector3(90f, 0f, 0f), new Vector3(0.062f, 0.008f, 0.062f), backingMaterial);
                CreatePrimitiveChild(sigil, PrimitiveType.Cylinder, "Core",    new Vector3(0f, 0f, 0.012f), new Vector3(90f, 0f, 0f), new Vector3(0.026f, 0.009f, 0.026f), accent);
                break;
        }
    }

    private static Vector3 AngleToPoint(float angleDegrees, float radius, float z)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians) * radius, Mathf.Cos(radians) * radius, z);
    }

    private static Material GetAccentMaterial(int dialIndex, MaterialSet materials)
    {
        return dialIndex switch
        {
            0 => materials.AccentBlue,
            1 => materials.AccentGreen,
            _ => materials.AccentCrimson,
        };
    }

    private static Transform RebuildGeneratedChild(Transform parent)
    {
        Transform existing = parent.Find(GeneratedChildName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        GameObject generatedObject = new GameObject(GeneratedChildName);
        Undo.RegisterCreatedObjectUndo(generatedObject, "Rebuild Church Dial Puzzle Default Visuals");
        generatedObject.transform.SetParent(parent, false);
        generatedObject.transform.localPosition = Vector3.zero;
        generatedObject.transform.localRotation = Quaternion.identity;
        generatedObject.transform.localScale = Vector3.one;
        return generatedObject.transform;
    }

    private static GameObject CreatePrimitiveChild(Transform parent, PrimitiveType primitiveType, string objectName, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        Undo.RegisterCreatedObjectUndo(primitive, "Create Church Dial Puzzle Default Visual");
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localEulerAngles = localEulerAngles;
        primitive.transform.localScale = localScale;

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return primitive;
    }

    private static Transform CreateEmptyChild(Transform parent, string objectName)
    {
        GameObject childObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(childObject, "Create Church Dial Puzzle Default Visual");
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        return CreateEmptyChild(parent, childName);
    }

    private static Transform ResolveRoot(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            if (LooksLikePuzzleRoot(current))
                return current;

            current = current.parent;
        }

        return candidate;
    }

    private static bool LooksLikePuzzleRoot(Transform transform)
    {
        if (transform == null)
            return false;

        return transform.Find(Dial1PivotName) != null ||
               transform.Find(Dial2PivotName) != null ||
               transform.Find(Dial3PivotName) != null ||
               transform.Find(PaintingVisualName) != null ||
               transform.Find(VaultDoorRootName) != null;
    }

    private static MaterialSet GetMaterialSet()
    {
        EnsureGeneratedFolders();
        Shader shader = FindPreferredLitShader();
        Shader unlitShader = FindPreferredUnlitShader();

        // Egyptian-themed palette:
        //   Stone     = warm sandstone / limestone
        //   StoneInset = shadowed dark sandstone
        //   Iron       = polished dark basalt
        //   Brass      = pharaoh gold
        //   BrassDark  = dark gold / ochre
        //   Wood       = ebony
        //   Parchment  = papyrus scroll
        //   PaintingArt= Egyptian painting (unlit, Eye-of-Horus texture)
        //   PaintedRed = terracotta / carnelian
        //   AccentBlue = lapis lazuli
        //   AccentGreen= faience turquoise-green
        //   AccentCrimson = carnelian red-orange
        //   Obsidian   = black onyx
        MaterialSet materials = new MaterialSet
        {
            // Emission colours are deliberately set to 25-40 % of base colour so that materials
            // always read as coloured even when the scene directional light is not facing the puzzle.
            Stone         = GetOrCreateMaterial("CDP_Stone",      shader,      new Color(0.72f, 0.63f, 0.46f), 0.03f, 0.20f, new Color(0.16f, 0.14f, 0.10f)),
            StoneInset    = GetOrCreateMaterial("CDP_StoneInset", shader,      new Color(0.40f, 0.33f, 0.20f), 0.04f, 0.24f, new Color(0.08f, 0.06f, 0.04f)),
            Iron          = GetOrCreateMaterial("CDP_Iron",       shader,      new Color(0.22f, 0.20f, 0.18f), 0.60f, 0.42f, new Color(0.05f, 0.05f, 0.04f)),
            Brass         = GetOrCreateMaterial("CDP_Brass",      shader,      new Color(0.82f, 0.65f, 0.17f), 0.96f, 0.74f, new Color(0.32f, 0.24f, 0.04f)),
            BrassDark     = GetOrCreateMaterial("CDP_BrassDark",  shader,      new Color(0.50f, 0.37f, 0.08f), 0.86f, 0.54f, new Color(0.16f, 0.11f, 0.02f)),
            Wood          = GetOrCreateMaterial("CDP_Wood",       shader,      new Color(0.18f, 0.12f, 0.07f), 0.08f, 0.30f, new Color(0.04f, 0.02f, 0.01f)),
            Parchment     = GetOrCreateMaterial("CDP_Parchment",  shader,      new Color(0.84f, 0.74f, 0.56f), 0.02f, 0.18f, new Color(0.20f, 0.16f, 0.10f)),
            PaintingArt   = GetOrCreateMaterial("CDP_PaintingArt",unlitShader, new Color(1f,    1f,    1f   ), 0f,    0f,    new Color(0.85f, 0.70f, 0.40f)),
            PaintedRed    = GetOrCreateMaterial("CDP_PaintedRed", shader,      new Color(0.68f, 0.26f, 0.10f), 0.05f, 0.28f, new Color(0.22f, 0.07f, 0.02f)),
            AccentBlue    = GetOrCreateMaterial("CDP_AccentBlue", shader,      new Color(0.18f, 0.30f, 0.72f), 0.14f, 0.44f, new Color(0.06f, 0.12f, 0.30f)),
            AccentGreen   = GetOrCreateMaterial("CDP_AccentGreen",shader,      new Color(0.20f, 0.62f, 0.54f), 0.16f, 0.46f, new Color(0.06f, 0.22f, 0.18f)),
            AccentCrimson = GetOrCreateMaterial("CDP_AccentCrimson",shader,    new Color(0.78f, 0.28f, 0.08f), 0.12f, 0.40f, new Color(0.28f, 0.08f, 0.02f)),
            Obsidian      = GetOrCreateMaterial("CDP_Obsidian",   shader,      new Color(0.10f, 0.09f, 0.11f), 0.46f, 0.64f, new Color(0.02f, 0.02f, 0.02f)),
        };

        Texture2D stoneTexture = GetOrCreateTexture("CDP_StoneTex.asset", 256, 256, GenerateStoneTexture, TextureWrapMode.Repeat);
        Texture2D stoneInsetTexture = GetOrCreateTexture("CDP_StoneInsetTex.asset", 256, 256, GenerateStoneInsetTexture, TextureWrapMode.Repeat);
        Texture2D ironTexture = GetOrCreateTexture("CDP_IronTex.asset", 256, 256, GenerateIronTexture, TextureWrapMode.Repeat);
        Texture2D brassTexture = GetOrCreateTexture("CDP_BrassTex.asset", 256, 256, GenerateBrassTexture, TextureWrapMode.Repeat);
        Texture2D woodTexture = GetOrCreateTexture("CDP_WoodTex.asset", 256, 256, GenerateWoodTexture, TextureWrapMode.Repeat);
        Texture2D parchmentTexture = GetOrCreateTexture("CDP_ParchmentTex.asset", 256, 256, GenerateParchmentTexture, TextureWrapMode.Clamp);
        Texture2D paintingTexture = GetOrCreateTexture("CDP_PaintingArtTex.asset", 512, 768, GeneratePaintingTexture, TextureWrapMode.Clamp);
        Texture2D obsidianTexture = GetOrCreateTexture("CDP_ObsidianTex.asset", 256, 256, GenerateObsidianTexture, TextureWrapMode.Repeat);

        ApplyBaseMap(materials.Stone, stoneTexture, new Vector2(3.5f, 4.8f));
        ApplyBaseMap(materials.StoneInset, stoneInsetTexture, new Vector2(2.4f, 3.2f));
        ApplyBaseMap(materials.Iron, ironTexture, new Vector2(1.5f, 1.5f));
        ApplyBaseMap(materials.Brass, brassTexture, new Vector2(1.25f, 1.25f));
        ApplyBaseMap(materials.BrassDark, brassTexture, new Vector2(1.15f, 1.15f));
        ApplyBaseMap(materials.Wood, woodTexture, new Vector2(1f, 1.75f));
        ApplyBaseMap(materials.Parchment, parchmentTexture, Vector2.one);
        ApplyBaseMap(materials.PaintingArt, paintingTexture, Vector2.one);
        ApplyBaseMap(materials.Obsidian, obsidianTexture, new Vector2(1.5f, 1.5f));

        return materials;
    }

    private static void EnsureGeneratedFolders()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedRootFolder))
            AssetDatabase.CreateFolder("Assets/Sushil/ChurchDialPuzzle", "Generated");

        if (!AssetDatabase.IsValidFolder(GeneratedMaterialFolder))
            AssetDatabase.CreateFolder(GeneratedRootFolder, "Materials");

        if (!AssetDatabase.IsValidFolder(GeneratedTextureFolder))
            AssetDatabase.CreateFolder(GeneratedRootFolder, "Textures");
    }

    // Delete every CDP_*.mat in the Generated/Materials folder so they are recreated from
    // scratch with the correct render-pipeline shader on the next GetMaterialSet() call.
    private static void PurgeMaterialAssets()
    {
        EnsureGeneratedFolders();
        string[] guids = AssetDatabase.FindAssets("t:Material CDP_", new[] { GeneratedMaterialFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.Refresh();
    }

    private static Material GetOrCreateMaterial(string assetName, Shader shader, Color baseColor, float metallic, float smoothness)
    {
        // No explicit emission colour — use a fraction of the base colour so the material
        // self-illuminates slightly and is always visible regardless of scene lighting direction.
        Color autoEmission = new Color(baseColor.r * 0.22f, baseColor.g * 0.22f, baseColor.b * 0.22f, 1f);
        return GetOrCreateMaterial(assetName, shader, baseColor, metallic, smoothness, autoEmission);
    }

    private static Material GetOrCreateMaterial(string assetName, Shader shader, Color baseColor, float metallic, float smoothness, Color emissionColor)
    {
        string path = $"{GeneratedMaterialFolder}/{assetName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader != null ? shader : FindPreferredLitShader());
            AssetDatabase.CreateAsset(material, path);
        }

        // Always re-assign the shader — the asset might have been created under a different
        // render pipeline (e.g. Standard vs URP/Lit), which causes materials to render black.
        if (shader != null && material.shader != shader)
            material.shader = shader;

        material.name = assetName;
        ApplyMaterialSettings(material, baseColor, metallic, smoothness, emissionColor);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyMaterialSettings(Material material, Color baseColor, float metallic, float smoothness, Color emissionColor)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        // Always enable emission. Even a small emission ensures the material colour is readable
        // if the scene directional light is not pointing at the puzzle face.
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor);
        }
    }

    private static void ApplyBaseMap(Material material, Texture2D texture, Vector2 tiling)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        if (material.HasProperty("_BaseMap_ST"))
            material.SetVector("_BaseMap_ST", new Vector4(tiling.x, tiling.y, 0f, 0f));

        material.mainTextureScale = tiling;
        EditorUtility.SetDirty(material);
    }

    private static Texture2D GetOrCreateTexture(string assetName, int width, int height, System.Action<Texture2D> generator, TextureWrapMode wrapMode)
    {
        string path = $"{GeneratedTextureFolder}/{assetName}";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(assetName),
                wrapMode = wrapMode,
                filterMode = FilterMode.Bilinear,
            };

            AssetDatabase.CreateAsset(texture, path);
        }

        texture.Reinitialize(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = wrapMode;
        texture.filterMode = FilterMode.Bilinear;
        generator?.Invoke(texture);
        texture.Apply(false, false);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    // Egyptian sandstone — warm layered limestone with subtle horizontal banding.
    private static void GenerateStoneTexture(Texture2D texture)
    {
        FillTexture(texture, (u, v) =>
        {
            // Coarse horizontal sedimentary banding
            float band = Mathf.PerlinNoise((u * 1.8f) + 3.4f, (v * 9.5f) + 1.1f);
            // Fine surface grit
            float grit = Mathf.PerlinNoise((u * 28f) + 5.1f, (v * 24f) + 9.8f);
            // Hairline fractures
            float frac = Mathf.Abs(Mathf.PerlinNoise((u * 11f) + 1.3f, (v * 7.5f) + 4.7f) - 0.5f);
            float crack = frac < 0.032f ? 1f - (frac / 0.032f) : 0f;

            Color sandLight = new Color(0.82f, 0.72f, 0.54f);
            Color sandMid   = new Color(0.65f, 0.56f, 0.38f);
            Color sandDark  = new Color(0.46f, 0.38f, 0.24f);

            Color col = Color.Lerp(sandMid, sandLight, band);
            col = Color.Lerp(col, sandDark, crack * 0.52f);
            return Color.Lerp(col, sandDark * 0.78f, grit * 0.10f);
        });
    }

    // Dark sandstone inset — shadowed carved recess.
    private static void GenerateStoneInsetTexture(Texture2D texture)
    {
        FillTexture(texture, (u, v) =>
        {
            float n1 = Mathf.PerlinNoise((u * 6.5f) + 2.2f, (v * 7.2f) + 3.1f);
            float n2 = Mathf.PerlinNoise((u * 24f) + 8.4f, (v * 24f) + 1.7f);
            Color baseColor = Color.Lerp(new Color(0.22f, 0.16f, 0.08f), new Color(0.42f, 0.34f, 0.20f), n1);
            return Color.Lerp(baseColor, new Color(0.60f, 0.48f, 0.28f), n2 * 0.10f);
        });
    }

    private static void GenerateIronTexture(Texture2D texture)
    {
        FillTexture(texture, (u, v) =>
        {
            float grain = Mathf.PerlinNoise((u * 45f) + 4.1f, 0.5f) * 0.18f;
            float n = Mathf.PerlinNoise((u * 7f) + 1.7f, (v * 7f) + 6.4f);
            float edge = Mathf.Abs(v - 0.5f);
            Color baseColor = Color.Lerp(new Color(0.12f, 0.13f, 0.15f), new Color(0.24f, 0.25f, 0.28f), n);
            baseColor += new Color(grain, grain, grain, 0f);
            return Color.Lerp(baseColor, new Color(0.06f, 0.06f, 0.07f), edge * 0.2f);
        });
    }

    // Egyptian gold — warm radial sheen with subtle hammered grain.
    private static void GenerateBrassTexture(Texture2D texture)
    {
        FillTexture(texture, (u, v) =>
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
            float radial = Mathf.Clamp01(1f - (p.magnitude * 1.6f));
            float ring = Mathf.Abs(Mathf.Sin(p.magnitude * 40f));
            float grain = Mathf.PerlinNoise((u * 18f) + 2.4f, (v * 18f) + 7.3f);
            Color dark = new Color(0.28f, 0.21f, 0.08f);
            Color bright = new Color(0.73f, 0.59f, 0.22f);
            Color color = Color.Lerp(dark, bright, radial * 0.75f + grain * 0.25f);
            return Color.Lerp(color, Color.white * 0.85f, ring * 0.06f);
        });
    }

    private static void GenerateWoodTexture(Texture2D texture)
    {
        FillTexture(texture, (u, v) =>
        {
            float bend = Mathf.Sin((v * 12f) + (Mathf.PerlinNoise(v * 1.4f, 2.2f) * 4f));
            float grain = Mathf.PerlinNoise((u * 28f) + (bend * 0.8f), (v * 5f) + 1.4f);
            float pores = Mathf.PerlinNoise((u * 75f) + 3.6f, (v * 22f) + 8.2f);
            Color dark = new Color(0.18f, 0.09f, 0.04f);
            Color light = new Color(0.38f, 0.22f, 0.11f);
            Color color = Color.Lerp(dark, light, grain);
            return Color.Lerp(color, Color.black, pores * 0.08f);
        });
    }

    private static void GenerateParchmentTexture(Texture2D texture)
    {
        FillTexture(texture, (u, v) =>
        {
            float noise = Mathf.PerlinNoise((u * 8f) + 0.7f, (v * 8f) + 1.3f);
            float stain = Mathf.PerlinNoise((u * 3f) + 4.4f, (v * 3f) + 5.8f);
            float edge = Mathf.Clamp01(1f - Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 4.2f);
            Color baseColor = Color.Lerp(new Color(0.58f, 0.51f, 0.37f), new Color(0.82f, 0.74f, 0.57f), noise);
            baseColor = Color.Lerp(baseColor, new Color(0.46f, 0.38f, 0.24f), stain * 0.16f);
            return Color.Lerp(baseColor, new Color(0.24f, 0.18f, 0.1f), edge * 0.42f);
        });
    }

    private static void GenerateObsidianTexture(Texture2D texture)
    {
        FillTexture(texture, (u, v) =>
        {
            float n = Mathf.PerlinNoise((u * 10f) + 3.3f, (v * 10f) + 8.7f);
            float glint = Mathf.PerlinNoise((u * 28f) + 2.1f, (v * 28f) + 6.1f);
            Color baseColor = Color.Lerp(new Color(0.03f, 0.03f, 0.04f), new Color(0.09f, 0.09f, 0.11f), n);
            return Color.Lerp(baseColor, new Color(0.16f, 0.14f, 0.18f), glint * 0.08f);
        });
    }

    // Egyptian painting — Eye of Horus (Wedjat) with sun disk (Ra), kohl markings,
    // papyrus-fibre background, gold cartouche border and hieroglyph-hint band.
    private static void GeneratePaintingTexture(Texture2D texture)
    {
        FillTexture(texture, (u, v) =>
        {
            Vector2 p = new Vector2(u, v);

            // ------ Background: aged papyrus ------
            float bgNoise = Mathf.PerlinNoise((u * 5.8f) + 2.1f, (v * 4.7f) + 3.6f);
            float ageSpot = Mathf.PerlinNoise((u * 2.8f) + 7.4f, (v * 2.5f) + 1.1f);
            Color papyrus = Color.Lerp(new Color(0.52f, 0.42f, 0.26f), new Color(0.76f, 0.64f, 0.44f), bgNoise);
            papyrus = Color.Lerp(papyrus, new Color(0.38f, 0.28f, 0.14f), ageSpot * 0.22f);

            // Horizontal papyrus fibre lines
            float fibre = Mathf.PerlinNoise(u * 0.4f, v * 90f) * 0.055f;
            papyrus += new Color(fibre, fibre * 0.80f, fibre * 0.45f, 0f);

            // Dark edge vignette — papyrus deterioration at borders
            float edgeDist = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float vignette = Mathf.Clamp01(1f - Mathf.Clamp01(edgeDist * 4.8f));
            Color color = Color.Lerp(papyrus, new Color(0.16f, 0.10f, 0.05f), vignette * 0.70f);

            // ------ Sun Disk of Ra — top centre ------
            Vector2 sunCtr = new Vector2(0.5f, 0.83f);
            float sunDisk  = EllipseMask(p, sunCtr, new Vector2(0.065f, 0.065f), 0.007f);
            color = Color.Lerp(color, new Color(0.88f, 0.68f, 0.12f), sunDisk * 0.96f);
            float sunCore  = EllipseMask(p, sunCtr, new Vector2(0.024f, 0.024f), 0.005f);
            color = Color.Lerp(color, new Color(0.08f, 0.06f, 0.03f), sunCore);
            // Outer glow corona ring
            float corona   = RingMask(p, sunCtr, 0.068f, 0.096f);
            color = Color.Lerp(color, new Color(0.78f, 0.55f, 0.08f), corona * 0.62f);
            // Four cardinal rays
            float rayH = RectMask(p, sunCtr + new Vector2(0f, 0.115f),  new Vector2(0.005f, 0.018f), 0.004f);
            float rayV = RectMask(p, sunCtr + new Vector2(0.115f, 0f),  new Vector2(0.018f, 0.005f), 0.004f);
            float rayHd= RectMask(p, sunCtr + new Vector2(0f, -0.115f), new Vector2(0.005f, 0.018f), 0.004f);
            float rayVd= RectMask(p, sunCtr + new Vector2(-0.115f, 0f), new Vector2(0.018f, 0.005f), 0.004f);
            color = Color.Lerp(color, new Color(0.84f, 0.62f, 0.10f),
                               Mathf.Clamp01(rayH + rayV + rayHd + rayVd) * 0.72f);

            // ------ Eye of Horus (Wedjat) — centre panel ------
            Vector2 eyeCtr = new Vector2(0.5f, 0.50f);

            // Kohl outline — slightly larger than the white, drawn first
            float kohlOutline = EllipseMask(p, eyeCtr, new Vector2(0.268f, 0.130f), 0.009f);
            color = Color.Lerp(color, new Color(0.06f, 0.04f, 0.02f), kohlOutline * 0.92f);

            // Eye white — ivory almond shape
            float eyeWhite = EllipseMask(p, eyeCtr, new Vector2(0.238f, 0.112f), 0.010f);
            color = Color.Lerp(color, new Color(0.94f, 0.89f, 0.76f), eyeWhite * 0.94f);

            // Gold iris ring
            float irisOuter = EllipseMask(p, eyeCtr, new Vector2(0.160f, 0.098f), 0.008f);
            float irisInner = EllipseMask(p, eyeCtr, new Vector2(0.085f, 0.092f), 0.007f);
            float iris = Mathf.Clamp01(irisOuter - irisInner) * eyeWhite;
            color = Color.Lerp(color, new Color(0.82f, 0.62f, 0.13f), iris * 0.90f);

            // Dark pupil — taller than wide (falcon slit)
            float pupil = EllipseMask(p, eyeCtr, new Vector2(0.055f, 0.088f), 0.007f);
            color = Color.Lerp(color, new Color(0.05f, 0.04f, 0.03f), pupil * 0.97f);

            // Pupil specular highlight
            float spec = EllipseMask(p, eyeCtr + new Vector2(-0.014f, 0.026f), new Vector2(0.016f, 0.012f), 0.004f);
            color = Color.Lerp(color, new Color(1.0f, 0.98f, 0.92f), spec * 0.72f);

            // Eyebrow — thick horizontal kohl bar above, offset left to angle
            float brow1 = RectMask(p, eyeCtr + new Vector2( 0.02f, 0.184f), new Vector2(0.21f, 0.017f), 0.013f);
            float brow2 = RectMask(p, eyeCtr + new Vector2(-0.06f, 0.202f), new Vector2(0.14f, 0.013f), 0.010f);
            color = Color.Lerp(color, new Color(0.06f, 0.04f, 0.02f),
                               Mathf.Clamp01(brow1 + brow2) * 0.93f);

            // Inner corner teardrop — vertical stroke down from inner corner
            float innerX = eyeCtr.x - 0.218f;
            float tdrop  = RectMask(p, new Vector2(innerX, eyeCtr.y - 0.096f), new Vector2(0.013f, 0.072f), 0.009f);
            float tfoot  = RectMask(p, new Vector2(innerX - 0.024f, eyeCtr.y - 0.188f), new Vector2(0.040f, 0.013f), 0.008f);
            color = Color.Lerp(color, new Color(0.06f, 0.04f, 0.02f),
                               Mathf.Clamp01(tdrop + tfoot) * 0.91f);

            // Outer kohl tail — horizontal extension right, then curl down
            float outerX = eyeCtr.x + 0.240f;
            float tail   = RectMask(p, new Vector2(outerX + 0.072f, eyeCtr.y),        new Vector2(0.072f, 0.012f), 0.008f);
            float curl   = RectMask(p, new Vector2(outerX + 0.144f, eyeCtr.y - 0.062f), new Vector2(0.012f, 0.062f), 0.008f);
            float foot   = RectMask(p, new Vector2(outerX + 0.106f, eyeCtr.y - 0.130f), new Vector2(0.050f, 0.012f), 0.007f);
            color = Color.Lerp(color, new Color(0.06f, 0.04f, 0.02f),
                               Mathf.Clamp01(tail + curl + foot) * 0.91f);

            // ------ Lapis blue band behind the eye (sky/night) ------
            float skyBand = RectMask(p, new Vector2(0.5f, 0.50f), new Vector2(0.46f, 0.145f), 0.030f);
            // Only colour the background portion that the eye does not cover
            float eyeCover = Mathf.Clamp01(eyeWhite + kohlOutline);
            color = Color.Lerp(color, new Color(0.14f, 0.22f, 0.56f), skyBand * (1f - eyeCover) * 0.55f);

            // Gold horizontal rule above sky band
            float topRule = RectMask(p, new Vector2(0.5f, 0.655f), new Vector2(0.44f, 0.010f), 0.006f);
            color = Color.Lerp(color, new Color(0.80f, 0.60f, 0.12f), topRule * 0.80f);

            // ------ Lower ankh sigil ------
            // Vertical stem
            float ankhStem = RectMask(p, new Vector2(0.5f, 0.275f), new Vector2(0.026f, 0.110f), 0.009f);
            // Crossbar
            float ankhArms = RectMask(p, new Vector2(0.5f, 0.330f), new Vector2(0.100f, 0.022f), 0.008f);
            // Loop — ring at top of stem
            float ankhLoop = RingMask(p, new Vector2(0.5f, 0.390f), 0.042f, 0.064f);
            color = Color.Lerp(color, new Color(0.82f, 0.62f, 0.12f),
                               Mathf.Clamp01(ankhStem + ankhArms + ankhLoop) * 0.88f);

            // ------ Hieroglyph hint band — bottom ------
            float hBand = RectMask(p, new Vector2(0.5f, 0.130f), new Vector2(0.40f, 0.030f), 0.005f);
            color = Color.Lerp(color, new Color(0.10f, 0.07f, 0.04f), hBand * 0.72f);
            // Small repeated glyph marks inside the band (unrolled to avoid per-pixel allocations)
            Color glyphGold = new Color(0.76f, 0.57f, 0.12f);
            color = Color.Lerp(color, glyphGold, RectMask(p, new Vector2(0.110f, 0.130f), new Vector2(0.014f, 0.018f), 0.003f) * 0.86f);
            color = Color.Lerp(color, glyphGold, RectMask(p, new Vector2(0.221f, 0.130f), new Vector2(0.014f, 0.018f), 0.003f) * 0.86f);
            color = Color.Lerp(color, glyphGold, RectMask(p, new Vector2(0.332f, 0.130f), new Vector2(0.014f, 0.018f), 0.003f) * 0.86f);
            color = Color.Lerp(color, glyphGold, RectMask(p, new Vector2(0.443f, 0.130f), new Vector2(0.014f, 0.018f), 0.003f) * 0.86f);
            color = Color.Lerp(color, glyphGold, RectMask(p, new Vector2(0.554f, 0.130f), new Vector2(0.014f, 0.018f), 0.003f) * 0.86f);
            color = Color.Lerp(color, glyphGold, RectMask(p, new Vector2(0.665f, 0.130f), new Vector2(0.014f, 0.018f), 0.003f) * 0.86f);
            color = Color.Lerp(color, glyphGold, RectMask(p, new Vector2(0.776f, 0.130f), new Vector2(0.014f, 0.018f), 0.003f) * 0.86f);
            color = Color.Lerp(color, glyphGold, RectMask(p, new Vector2(0.887f, 0.130f), new Vector2(0.014f, 0.018f), 0.003f) * 0.86f);

            // ------ Gold cartouche border ------
            float topBorder  = RectMask(p, new Vector2(0.5f, 0.930f), new Vector2(0.44f, 0.016f), 0.007f);
            float botBorder  = RectMask(p, new Vector2(0.5f, 0.060f), new Vector2(0.44f, 0.016f), 0.007f);
            float leftBorder = RectMask(p, new Vector2(0.075f, 0.5f), new Vector2(0.016f, 0.44f), 0.007f);
            float rightBorder= RectMask(p, new Vector2(0.925f, 0.5f), new Vector2(0.016f, 0.44f), 0.007f);
            color = Color.Lerp(color, new Color(0.82f, 0.62f, 0.13f),
                               Mathf.Clamp01(topBorder + botBorder + leftBorder + rightBorder) * 0.78f);

            // Corner lotus rosettes (unrolled — no per-pixel allocations)
            Color rosette = new Color(0.86f, 0.68f, 0.16f);
            Vector2 rSize  = new Vector2(0.030f, 0.030f);
            color = Color.Lerp(color, rosette, EllipseMask(p, new Vector2(0.076f, 0.930f), rSize, 0.008f) * 0.88f);
            color = Color.Lerp(color, rosette, EllipseMask(p, new Vector2(0.924f, 0.930f), rSize, 0.008f) * 0.88f);
            color = Color.Lerp(color, rosette, EllipseMask(p, new Vector2(0.076f, 0.060f), rSize, 0.008f) * 0.88f);
            color = Color.Lerp(color, rosette, EllipseMask(p, new Vector2(0.924f, 0.060f), rSize, 0.008f) * 0.88f);

            return color;
        });
    }

    private static void FillTexture(Texture2D texture, System.Func<float, float, Color> pixelFunc)
    {
        int width = texture.width;
        int height = texture.height;
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = height > 1 ? y / (float)(height - 1) : 0f;
            for (int x = 0; x < width; x++)
            {
                float u = width > 1 ? x / (float)(width - 1) : 0f;
                pixels[(y * width) + x] = pixelFunc(u, v);
            }
        }

        texture.SetPixels(pixels);
    }

    private static float RectMask(Vector2 uv, Vector2 center, Vector2 halfSize, float feather)
    {
        float dx = Mathf.Abs(uv.x - center.x) - halfSize.x;
        float dy = Mathf.Abs(uv.y - center.y) - halfSize.y;
        float distance = Mathf.Max(dx, dy);
        return 1f - Mathf.Clamp01(distance / Mathf.Max(0.0001f, feather));
    }

    private static float EllipseMask(Vector2 uv, Vector2 center, Vector2 radius, float feather)
    {
        Vector2 delta = new Vector2((uv.x - center.x) / Mathf.Max(0.0001f, radius.x), (uv.y - center.y) / Mathf.Max(0.0001f, radius.y));
        float distance = delta.magnitude;
        return 1f - Mathf.Clamp01((distance - 1f) / Mathf.Max(0.0001f, feather));
    }

    private static float RingMask(Vector2 uv, Vector2 center, float innerRadius, float outerRadius)
    {
        float distance = Vector2.Distance(uv, center);
        if (distance < innerRadius || distance > outerRadius)
            return 0f;

        float mid = (innerRadius + outerRadius) * 0.5f;
        float width = (outerRadius - innerRadius) * 0.5f;
        return 1f - Mathf.Clamp01(Mathf.Abs(distance - mid) / Mathf.Max(0.0001f, width));
    }

    private static float ArcMask(Vector2 uv, Vector2 center, float innerRadius, float outerRadius, float minDegrees, float maxDegrees, float feather)
    {
        Vector2 delta = uv - center;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;

        if (angle < minDegrees || angle > maxDegrees)
            return 0f;

        float distance = delta.magnitude;
        if (distance < innerRadius || distance > outerRadius)
            return 0f;

        float innerFade = Mathf.Clamp01((distance - innerRadius) / Mathf.Max(0.0001f, feather));
        float outerFade = 1f - Mathf.Clamp01((distance - (outerRadius - feather)) / Mathf.Max(0.0001f, feather));
        return innerFade * outerFade;
    }

    private static Shader FindPreferredLitShader()
    {
        // 1. Canonical URP name — works in Unity 2019 through Unity 6
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
            return shader;

        // 2. Fetch via the active render pipeline asset's default material.
        //    This works even when Shader.Find fails because the shader variant has been
        //    stripped or isn't yet in the shader database during editor initialisation.
        var pipeline = GraphicsSettings.defaultRenderPipeline;
        if (pipeline != null)
        {
            Material pipelineMat = pipeline.defaultMaterial;
            if (pipelineMat != null && pipelineMat.shader != null &&
                !pipelineMat.shader.name.Equals("Standard"))
                return pipelineMat.shader;
        }

        // 3. Scan existing project materials for one already using a URP Lit shader.
        //    Borrowing the shader reference avoids the string-lookup entirely.
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || path.Contains("CDP_"))
                continue;
            Material candidate = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (candidate == null || candidate.shader == null)
                continue;
            string sn = candidate.shader.name;
            if (sn.StartsWith("Universal Render Pipeline") && !sn.Contains("Unlit"))
                return candidate.shader;
        }

        // 4. Last resort — Standard shader only works with the Built-in RP.
        //    In a URP project it will render as solid pink/magenta.
        Debug.LogWarning("[ChurchDialPuzzle] Could not find Universal Render Pipeline/Lit shader. " +
                         "Materials may appear pink. Verify that the URP package is installed and " +
                         "at least one material in the project already uses URP/Lit.");
        return Shader.Find("Standard") ?? Shader.Find("Diffuse");
    }

    private static Shader FindPreferredUnlitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
            return shader;

        // Try pipeline default unlit material
        var pipeline = GraphicsSettings.defaultRenderPipeline;
        if (pipeline != null)
        {
            Material unlitMat = pipeline.defaultUIMaterial;
            if (unlitMat != null && unlitMat.shader != null &&
                unlitMat.shader.name.Contains("Universal"))
                return unlitMat.shader;
        }

        // Scan project for a URP Unlit material
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || path.Contains("CDP_"))
                continue;
            Material candidate = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (candidate == null || candidate.shader == null)
                continue;
            string sn = candidate.shader.name;
            if (sn.StartsWith("Universal Render Pipeline") && sn.Contains("Unlit"))
                return candidate.shader;
        }

        shader = Shader.Find("Unlit/Texture");
        if (shader != null)
            return shader;

        shader = Shader.Find("Sprites/Default");
        if (shader != null)
            return shader;

        return FindPreferredLitShader();
    }
}
