using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HorrorHouseBuilder
{
    private const float ExteriorWallThk = 0.25f;
    private const float InteriorWallThk = 0.18f;
    private const float GfWallHeight = 3f;
    private const float UpWallHeight = 3f;
    private const float BsWallHeight = 2.4f;
    private const float FloorThk = 0.1f;

    private static GameObject CreateBox(string name, Vector3 pos, Vector3 scale, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        return go;
    }

    private static GameObject WallX(string name, float x1, float x2, float z, float height, float thickness, float baseY, Transform parent)
    {
        var len = Mathf.Abs(x2 - x1);
        if (len <= 0f) return null;
        var center = new Vector3((x1 + x2) * 0.5f, baseY + height * 0.5f, z);
        var scale = new Vector3(len, height, thickness);
        return CreateBox(name, center, scale, parent);
    }

    private static GameObject WallZ(string name, float x, float z1, float z2, float height, float thickness, float baseY, Transform parent)
    {
        var len = Mathf.Abs(z2 - z1);
        if (len <= 0f) return null;
        var center = new Vector3(x, baseY + height * 0.5f, (z1 + z2) * 0.5f);
        var scale = new Vector3(thickness, height, len);
        return CreateBox(name, center, scale, parent);
    }

    private static GameObject Floor(string name, float xSize, float zSize, float baseY, Transform parent)
    {
        var pos = new Vector3(0f, baseY - FloorThk * 0.5f, 0f);
        var scale = new Vector3(xSize, FloorThk, zSize);
        return CreateBox(name, pos, scale, parent);
    }

    [MenuItem("Tools/Build Horror House Blockout")]
    public static void Build()
    {
        // work on active scene; if none, fall back
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            var fallback = "Assets/Scenes/SahilLevelDesign.unity";
            if (!System.IO.File.Exists(fallback))
            {
                fallback = "Assets/Scenes/SampleScene.unity";
            }
            scene = EditorSceneManager.OpenScene(fallback, OpenSceneMode.Single);
        }

        // remove previous build
        var existing = GameObject.Find("HorrorHouse_Blockout");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject("HorrorHouse_Blockout");

        var ground = new GameObject("GroundFloor");
        ground.transform.SetParent(root.transform);
        var upstairs = new GameObject("Upstairs");
        upstairs.transform.SetParent(root.transform);
        var basement = new GameObject("Basement");
        basement.transform.SetParent(root.transform);
        var staircase = new GameObject("Staircase");
        staircase.transform.SetParent(root.transform);
        var shell = new GameObject("Shell");
        shell.transform.SetParent(root.transform);
        var lights = new GameObject("Lights");
        lights.transform.SetParent(root.transform);
        var debug = new GameObject("DebugMarkers");
        debug.transform.SetParent(root.transform);

        // sub-groups ground
        var gfFloors = new GameObject("GF_Floors");
        gfFloors.transform.SetParent(ground.transform);
        var gfExt = new GameObject("GF_Walls_Exterior");
        gfExt.transform.SetParent(ground.transform);
        var gfInt = new GameObject("GF_Walls_Interior");
        gfInt.transform.SetParent(ground.transform);
        var gfOpen = new GameObject("GF_Openings");
        gfOpen.transform.SetParent(ground.transform);
        var gfMarkers = new GameObject("GF_RoomMarkers");
        gfMarkers.transform.SetParent(ground.transform);

        // sub-groups upstairs
        var upFloors = new GameObject("UP_Floors");
        upFloors.transform.SetParent(upstairs.transform);
        var upExt = new GameObject("UP_Walls_Exterior");
        upExt.transform.SetParent(upstairs.transform);
        var upInt = new GameObject("UP_Walls_Interior");
        upInt.transform.SetParent(upstairs.transform);
        var upOpen = new GameObject("UP_Openings");
        upOpen.transform.SetParent(upstairs.transform);
        var upMarkers = new GameObject("UP_RoomMarkers");
        upMarkers.transform.SetParent(upstairs.transform);

        // sub-groups basement
        var bsFloors = new GameObject("BS_Floors");
        bsFloors.transform.SetParent(basement.transform);
        var bsExt = new GameObject("BS_Walls_Exterior");
        bsExt.transform.SetParent(basement.transform);
        var bsInt = new GameObject("BS_Walls_Interior");
        bsInt.transform.SetParent(basement.transform);
        var bsOpen = new GameObject("BS_Openings");
        bsOpen.transform.SetParent(basement.transform);
        var bsMarkers = new GameObject("BS_RoomMarkers");
        bsMarkers.transform.SetParent(basement.transform);

        // staircase sub-groups
        var stairsUp = new GameObject("Stairs_GroundToUp");
        stairsUp.transform.SetParent(staircase.transform);
        var stairsDown = new GameObject("Stairs_GroundToBasement");
        stairsDown.transform.SetParent(staircase.transform);
        var landings = new GameObject("Landings");
        landings.transform.SetParent(staircase.transform);

        // floors
        Floor("GF_MainFloor", 18f, 14f, 0f, gfFloors.transform);
        Floor("BS_MainFloor", 18f, 14f, -2.6f, bsFloors.transform);

        // exterior walls ground
        float xMin = -9f + ExteriorWallThk * 0.5f; // -8.875
        float xMax = 9f - ExteriorWallThk * 0.5f;  // 8.875
        float zMin = -7f + ExteriorWallThk * 0.5f; // -6.875
        float zMax = 7f - ExteriorWallThk * 0.5f;  // 6.875

        // south wall with entrance gap
        WallX("GF_South_Wall_Left", -9f, -0.45f, -7f, GfWallHeight, ExteriorWallThk, 0f, gfExt.transform);
        WallX("GF_South_Wall_Right", 0.45f, 9f, -7f, GfWallHeight, ExteriorWallThk, 0f, gfExt.transform);
        WallZ("GF_West_Wall", -9f, -7f, 7f, GfWallHeight, ExteriorWallThk, 0f, gfExt.transform);
        WallZ("GF_East_Wall", 9f, -7f, 7f, GfWallHeight, ExteriorWallThk, 0f, gfExt.transform);
        WallX("GF_North_Wall", -9f, 9f, 7f, GfWallHeight, ExteriorWallThk, 0f, gfExt.transform);

        // ground floor interior layout numbers (interior extents)
        float ehX1 = -2f, ehX2 = 2f, ehZ1 = -6.75f, ehZ2 = -3.75f;
        float lvX1 = -8.75f, lvX2 = -1.75f, lvZ1 = -3.75f, lvZ2 = 1.25f;
        float dnX1 = -8.75f, dnX2 = -4.75f, dnZ1 = 1.25f, dnZ2 = 4.75f;
        float ktX1 = 2.75f, ktX2 = 6.75f, ktZ1 = -1.75f, ktZ2 = 2.25f;
        float bathX1 = 0.75f, bathX2 = 2.75f, bathZ1 = 1.25f, bathZ2 = 3.25f;
        float storX1 = -4.75f, storX2 = -3.25f, storZ1 = -2.75f, storZ2 = -0.75f;
        float stairX1 = -4.75f, stairX2 = -2.25f, stairZ1 = 0.75f, stairZ2 = 4.75f;

        // entrance hall walls
        WallZ("GF_EH_West", ehX1, ehZ1, ehZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_EH_East", ehX2, ehZ1, ehZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_EH_North_Left", ehX1, -0.45f, ehZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_EH_North_Right", 0.45f, ehX2, ehZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);

        // living room walls
        WallZ("GF_LV_West", lvX1, lvZ1, lvZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_LV_East_South", lvX2, lvZ1, -1.45f, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_LV_East_North", lvX2, -0.55f, lvZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_LV_North_Left", lvX1, -7.45f, lvZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_LV_North_Right", -6.55f, lvX2, lvZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);

        // dining room walls
        WallZ("GF_DN_West", dnX1, dnZ1, dnZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_DN_East", dnX2, dnZ1, dnZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_DN_North", dnX1, dnX2, dnZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        // optional east opening toward corridor (none to keep tight)

        // kitchen walls
        WallZ("GF_KT_West_South", ktX1, ktZ1, 0.55f, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_KT_West_North", ktX1, 1.45f, ktZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_KT_East", ktX2, ktZ1, ktZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_KT_North", ktX1, ktX2, ktZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_KT_South", ktX1, ktX2, ktZ1, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);

        // bathroom walls
        WallZ("GF_BATH_West", bathX1, bathZ1, bathZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_BATH_East", bathX2, bathZ1, bathZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_BATH_North", bathX1, bathX2, bathZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_BATH_South_Left", bathX1, 1.3f, bathZ1, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_BATH_South_Right", 2.2f, bathX2, bathZ1, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);

        // storage walls
        WallZ("GF_STOR_West", storX1, storZ1, storZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_STOR_East_Top", storX2, -1.25f, storZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_STOR_East_Bot", storX2, storZ1, -2.25f, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_STOR_North", storX1, storX2, storZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_STOR_South", storX1, storX2, storZ1, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);

        // staircase shaft walls
        WallZ("GF_STAIR_West", stairX1, stairZ1, stairZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallZ("GF_STAIR_East", stairX2, stairZ1, stairZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        WallX("GF_STAIR_North", stairX1, stairX2, stairZ2, GfWallHeight, InteriorWallThk, 0f, gfInt.transform);
        // south intentionally open for circulation

        // markers ground
        CreateMarker("Marker_EntranceHall", new Vector3(0f, 0.5f, -5.25f), gfMarkers.transform);
        CreateMarker("Marker_LivingRoom", new Vector3(-5.25f, 0.5f, -1.25f), gfMarkers.transform);
        CreateMarker("Marker_Kitchen", new Vector3(4.75f, 0.5f, 0.25f), gfMarkers.transform);
        CreateMarker("Marker_DiningRoom", new Vector3(-6.75f, 0.5f, 3f), gfMarkers.transform);
        CreateMarker("Marker_Bathroom", new Vector3(1.75f, 0.5f, 2.25f), gfMarkers.transform);
        CreateMarker("Marker_Storage", new Vector3(-4f, 0.5f, -1.75f), gfMarkers.transform);

        // upstairs exterior walls
        WallX("UP_South_Wall", -9f, 9f, -7f, UpWallHeight, ExteriorWallThk, 3.2f, upExt.transform);
        WallX("UP_North_Wall", -9f, 9f, 7f, UpWallHeight, ExteriorWallThk, 3.2f, upExt.transform);
        WallZ("UP_West_Wall", -9f, -7f, 7f, UpWallHeight, ExteriorWallThk, 3.2f, upExt.transform);
        WallZ("UP_East_Wall", 9f, -7f, 7f, UpWallHeight, ExteriorWallThk, 3.2f, upExt.transform);

        // upstairs rooms
        float hallX1 = -0.8f, hallX2 = 0.8f, hallZ1 = -3f, hallZ2 = 5f;
        float mbX1 = -8.75f, mbX2 = -3.75f, mbZ1 = 1.75f, mbZ2 = 6.25f;
        float chX1 = -8.0f, chX2 = -4.5f, chZ1 = -5.5f, chZ2 = -2.0f;
        float stX1 = 4.0f, stX2 = 8.0f, stZ1 = -1.0f, stZ2 = 2.5f;
        float balconyX1 = 8.0f, balconyX2 = 11.0f, balconyZ1 = -0.75f, balconyZ2 = 0.75f;

        // upstairs floors per room for clarity
        CreateBox("UP_Floor_Hall", new Vector3((hallX1 + hallX2) * 0.5f, 3.2f - FloorThk * 0.5f, (hallZ1 + hallZ2) * 0.5f), new Vector3(hallX2 - hallX1, FloorThk, hallZ2 - hallZ1), upFloors.transform);
        CreateBox("UP_Floor_Master", new Vector3((mbX1 + mbX2) * 0.5f, 3.2f - FloorThk * 0.5f, (mbZ1 + mbZ2) * 0.5f), new Vector3(mbX2 - mbX1, FloorThk, mbZ2 - mbZ1), upFloors.transform);
        CreateBox("UP_Floor_Child", new Vector3((chX1 + chX2) * 0.5f, 3.2f - FloorThk * 0.5f, (chZ1 + chZ2) * 0.5f), new Vector3(chX2 - chX1, FloorThk, chZ2 - chZ1), upFloors.transform);
        CreateBox("UP_Floor_Study", new Vector3((stX1 + stX2) * 0.5f, 3.2f - FloorThk * 0.5f, (stZ1 + stZ2) * 0.5f), new Vector3(stX2 - stX1, FloorThk, stZ2 - stZ1), upFloors.transform);
        CreateBox("UP_Floor_Balcony", new Vector3((balconyX1 + balconyX2) * 0.5f, 3.2f - FloorThk * 0.5f, (balconyZ1 + balconyZ2) * 0.5f), new Vector3(balconyX2 - balconyX1, FloorThk, balconyZ2 - balconyZ1), upFloors.transform);

        // upstairs interior walls
        // master bedroom
        WallZ("UP_MB_West", mbX1, mbZ1, mbZ2, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);
        WallZ("UP_MB_East", mbX2, mbZ1, mbZ2, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);
        WallX("UP_MB_North", mbX1, mbX2, mbZ2, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);
        WallXWithDoor("UP_MB_South", mbX1, mbX2, mbZ1, UpWallHeight, InteriorWallThk, 3.2f, -6.1f, upInt.transform);

        // child room
        WallZ("UP_CH_West", chX1, chZ1, chZ2, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);
        WallZWithDoor("UP_CH_East", chX2, chZ1, chZ2, UpWallHeight, InteriorWallThk, 3.2f, -3.5f, upInt.transform);
        WallX("UP_CH_North", chX1, chX2, chZ2, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);
        WallX("UP_CH_South", chX1, chX2, chZ1, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);

        // study with balcony door
        WallZWithDoor("UP_ST_West", stX1, stZ1, stZ2, UpWallHeight, InteriorWallThk, 3.2f, 0.75f, upInt.transform);
        WallZ("UP_ST_East_Left", stX2, stZ1, -0.45f, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);
        WallZ("UP_ST_East_Right", stX2, 0.45f, stZ2, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);
        WallX("UP_ST_North", stX1, stX2, stZ2, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);
        WallX("UP_ST_South", stX1, stX2, stZ1, UpWallHeight, InteriorWallThk, 3.2f, upInt.transform);

        // upstairs hallway helper walls for spine edges
        WallZ("UP_Hall_West", hallX1, hallZ1, hallZ2, UpWallHeight, InteriorWallThk * 0.5f, 3.2f, upInt.transform);
        WallZ("UP_Hall_East", hallX2, hallZ1, hallZ2, UpWallHeight, InteriorWallThk * 0.5f, 3.2f, upInt.transform);

        // balcony outer rim
        WallX("UP_Balcony_North", balconyX1, balconyX2, balconyZ2, 1f, InteriorWallThk, 3.2f, upInt.transform);
        WallX("UP_Balcony_South", balconyX1, balconyX2, balconyZ1, 1f, InteriorWallThk, 3.2f, upInt.transform);
        WallZ("UP_Balcony_East", balconyX2, balconyZ1, balconyZ2, 1f, InteriorWallThk, 3.2f, upInt.transform);

        // markers upstairs
        CreateMarker("Marker_MasterBedroom", new Vector3(-6.25f, 3.7f, 4f), upMarkers.transform);
        CreateMarker("Marker_ChildRoom", new Vector3(-6.25f, 3.7f, -3.75f), upMarkers.transform);
        CreateMarker("Marker_Study", new Vector3(6f, 3.7f, 0.75f), upMarkers.transform);
        CreateMarker("Marker_Balcony", new Vector3(9.5f, 3.3f, 0f), upMarkers.transform);

        // basement exterior walls
        WallX("BS_South_Wall", -9f, 9f, -7f, BsWallHeight, ExteriorWallThk, -2.6f, bsExt.transform);
        WallX("BS_North_Wall", -9f, 9f, 7f, BsWallHeight, ExteriorWallThk, -2.6f, bsExt.transform);
        WallZ("BS_West_Wall", -9f, -7f, 7f, BsWallHeight, ExteriorWallThk, -2.6f, bsExt.transform);
        WallZ("BS_East_Wall", 9f, -7f, 7f, BsWallHeight, ExteriorWallThk, -2.6f, bsExt.transform);

        float genX1 = -8.75f, genX2 = -3.75f, genZ1 = -6f, genZ2 = -2f;
        float boilerX1 = 3.25f, boilerX2 = 6.75f, boilerZ1 = -6f, boilerZ2 = -3f;
        float mazeX1 = -1f, mazeX2 = 5f, mazeZ1 = 0f, mazeZ2 = 4f;
        float crawlX = -3.25f, crawlZ1 = -3f, crawlZ2 = 2f;

        // generator room walls
        WallZ("BS_GEN_West", genX1, genZ1, genZ2, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);
        WallZWithDoor("BS_GEN_East", genX2, genZ1, genZ2, BsWallHeight, InteriorWallThk, -2.6f, -4f, bsInt.transform);
        WallX("BS_GEN_North", genX1, genX2, genZ2, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);
        WallX("BS_GEN_South", genX1, genX2, genZ1, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);

        // boiler room
        WallZWithDoor("BS_BOIL_West", boilerX1, boilerZ1, boilerZ2, BsWallHeight, InteriorWallThk, -2.6f, -4.5f, bsInt.transform);
        WallZ("BS_BOIL_East", boilerX2, boilerZ1, boilerZ2, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);
        WallX("BS_BOIL_North", boilerX1, boilerX2, boilerZ2, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);
        WallX("BS_BOIL_South", boilerX1, boilerX2, boilerZ1, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);

        // storage maze
        WallZWithDoor("BS_MAZE_West", mazeX1, mazeZ1, mazeZ2, BsWallHeight, InteriorWallThk, -2.6f, 1f, bsInt.transform);
        WallZ("BS_MAZE_East", mazeX2, mazeZ1, mazeZ2, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);
        WallX("BS_MAZE_North", mazeX1, mazeX2, mazeZ2, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);
        WallX("BS_MAZE_South", mazeX1, mazeX2, mazeZ1, BsWallHeight, InteriorWallThk, -2.6f, bsInt.transform);

        // crawl tunnel cube
        var crawlCenter = new Vector3(crawlX, -2.6f + 0.7f, (crawlZ1 + crawlZ2) * 0.5f);
        var crawlSize = new Vector3(1f, 1.4f, Mathf.Abs(crawlZ2 - crawlZ1));
        CreateBox("CrawlTunnel", crawlCenter, crawlSize, basement.transform);
        CreateMarker("Marker_CrawlTunnel", crawlCenter + new Vector3(0, 0.7f, 0), bsMarkers.transform);

        // markers basement
        CreateMarker("Marker_GeneratorRoom", new Vector3(-6.25f, -2.0f, -4f), bsMarkers.transform);
        CreateMarker("Marker_BoilerRoom", new Vector3(5f, -2.0f, -4.5f), bsMarkers.transform);
        CreateMarker("Marker_StorageMaze", new Vector3(2f, -2.0f, 2f), bsMarkers.transform);

        // lights per major room
        AddLight("Light_EntranceHall", new Vector3(0f, 2.6f, -5.25f), lights.transform);
        AddLight("Light_Living", new Vector3(-5.25f, 2.6f, -1.25f), lights.transform);
        AddLight("Light_Kitchen", new Vector3(4.75f, 2.6f, 0.25f), lights.transform);
        AddLight("Light_Dining", new Vector3(-6.75f, 2.6f, 3f), lights.transform);
        AddLight("Light_Bathroom", new Vector3(1.75f, 2.6f, 2.25f), lights.transform);
        AddLight("Light_Storage", new Vector3(-4f, 2.6f, -1.75f), lights.transform);
        AddLight("Light_Master", new Vector3(-6.25f, 6.7f, 4f), lights.transform);
        AddLight("Light_Child", new Vector3(-6.25f, 6.7f, -3.75f), lights.transform);
        AddLight("Light_Study", new Vector3(6f, 6.7f, 0.75f), lights.transform);
        AddLight("Light_Generator", new Vector3(-6.25f, -0.6f, -4f), lights.transform);
        AddLight("Light_Boiler", new Vector3(5f, -0.6f, -4.5f), lights.transform);
        AddLight("Light_StorageMaze", new Vector3(2f, -0.6f, 2f), lights.transform);

        // staircase geometry (simple stepped blocks)
        BuildStairs(stairsUp.transform, true);
        BuildStairs(stairsDown.transform, false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Horror house blockout generated in scene: " + scene.path);
    }

    private static void BuildStairs(Transform parent, bool up)
    {
        float width = 1.2f;
        float runLength = 1.4f;
        float landingDepth = 1.5f;
        int steps = 6;
        float riser = up ? (3.2f / (steps * 2)) : (-2.6f / (steps * 2));
        float tread = runLength / steps;
        float startY = 0f;
        float dir = up ? 1f : -1f;
        float baseZ = up ? 0.75f : 4.75f; // keep inside shaft
        float startX = -3.5f; // centered in shaft

        // first run
        for (int i = 0; i < steps; i++)
        {
            float yMid = startY + riser * (i + 0.5f);
            float zMid = baseZ + dir * (tread * (i + 0.5f));
            CreateBox((up ? "Up" : "Down") + "_StepA_" + i, new Vector3(startX, yMid, zMid), new Vector3(width, Mathf.Abs(riser), tread), parent);
        }

        // landing
        float landingY = startY + riser * steps;
        float landingZ = baseZ + dir * runLength + dir * landingDepth * 0.5f;
        CreateBox((up ? "Up" : "Down") + "_Landing", new Vector3(startX, landingY, landingZ), new Vector3(width, FloorThk, landingDepth), parent.parent.Find("Landings"));

        // second run (reverse direction)
        for (int i = 0; i < steps; i++)
        {
            float yMid = landingY + riser * (i + 0.5f);
            float zMid = landingZ + dir * landingDepth * 0.5f - dir * (tread * (i + 0.5f));
            CreateBox((up ? "Up" : "Down") + "_StepB_" + i, new Vector3(startX, yMid, zMid), new Vector3(width, Mathf.Abs(riser), tread), parent);
        }
    }

    private static void AddLight(string name, Vector3 pos, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = 3f;
        light.range = 8f;
    }

    private static void CreateMarker(string name, Vector3 pos, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
    }

    private static void WallZWithDoor(string name, float x, float z1, float z2, float height, float thickness, float baseY, float doorCenterZ, Transform parent)
    {
        float doorHalf = 0.45f;
        float lower = Mathf.Min(z1, z2);
        float upper = Mathf.Max(z1, z2);
        float seg1End = doorCenterZ - doorHalf;
        float seg2Start = doorCenterZ + doorHalf;
        if (seg1End > lower)
        {
            WallZ(name + "_Lower", x, lower, seg1End, height, thickness, baseY, parent);
        }
        if (upper > seg2Start)
        {
            WallZ(name + "_Upper", x, seg2Start, upper, height, thickness, baseY, parent);
        }
    }

    private static void WallXWithDoor(string name, float x1, float x2, float z, float height, float thickness, float baseY, float doorCenterX, Transform parent)
    {
        float doorHalf = 0.45f;
        float lower = Mathf.Min(x1, x2);
        float upper = Mathf.Max(x1, x2);
        float seg1End = doorCenterX - doorHalf;
        float seg2Start = doorCenterX + doorHalf;
        if (seg1End > lower)
        {
            WallX(name + "_Left", lower, seg1End, z, height, thickness, baseY, parent);
        }
        if (upper > seg2Start)
        {
            WallX(name + "_Right", seg2Start, upper, z, height, thickness, baseY, parent);
        }
    }

    [InitializeOnLoadMethod]
    private static void AutoBuildOnCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode) return;
            Build();
        };
    }
}
