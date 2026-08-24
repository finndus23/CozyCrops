#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Baut die zur Laufzeit erzeugten Teile der Farm als nicht gespeicherte Editor-Vorschau
/// nach. Dadurch bleibt die Scene-Datei klein, waehrend Scene View und Play Mode dieselbe
/// Farmgroesse und dieselbe deterministische Kleindeko zeigen.
/// </summary>
[InitializeOnLoad]
public static class FarmSceneEditorPreview
{
    private const string PreviewRootName = "__Farm Editor Preview (wird nicht gespeichert)";
    private const HideFlags PreviewFlags =
        HideFlags.HideInHierarchy | HideFlags.NotEditable | HideFlags.DontSaveInEditor;

    private static bool refreshQueued;
    private static bool isRefreshing;
    private static readonly List<GameObject> HiddenForPreview = new();

    static FarmSceneEditorPreview()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.projectChanged += QueueRefresh;
        AssemblyReloadEvents.beforeAssemblyReload += ClearAllPreviews;
        QueueRefresh();
    }

    [MenuItem("Tools/CozyCrops/Farm-Vorschau aktualisieren")]
    public static void RefreshNow()
    {
        refreshQueued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        RefreshAllPreviews();
    }

    [MenuItem("Tools/CozyCrops/Farm-Vorschau ausblenden")]
    public static void ClearAllPreviews()
    {
        if (isRefreshing)
            return;

        foreach (GridManager grid in FindLoadedGridManagers())
            ClearPreview(grid);

        RestoreSceneVisibility();
        SceneView.RepaintAll();
    }

    public static void QueueRefresh()
    {
        if (refreshQueued || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        refreshQueued = true;
        EditorApplication.delayCall += () =>
        {
            refreshQueued = false;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                RefreshAllPreviews();
        };
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => QueueRefresh();

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            ClearAllPreviews();
        else if (state == PlayModeStateChange.EnteredEditMode)
            QueueRefresh();
    }

    private static void RefreshAllPreviews()
    {
        if (isRefreshing)
            return;

        isRefreshing = true;
        try
        {
            RestoreSceneVisibility();
            foreach (GridManager grid in FindLoadedGridManagers())
                BuildPreview(grid);
        }
        finally
        {
            isRefreshing = false;
            SceneView.RepaintAll();
        }
    }

    private static GridManager[] FindLoadedGridManagers() =>
        UnityEngine.Object.FindObjectsByType<GridManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

    private static void BuildPreview(GridManager grid)
    {
        if (grid == null || !grid.gameObject.scene.IsValid() || !grid.gameObject.scene.isLoaded)
            return;

        ClearPreview(grid);

        var serializedGrid = new SerializedObject(grid);
        GameObject grassPrefab = ReadObject<GameObject>(serializedGrid, "grassTilePrefab");
        if (grassPrefab == null)
            return;

        var previewRootObject = new GameObject(PreviewRootName)
        {
            tag = "EditorOnly",
            hideFlags = PreviewFlags
        };
        Transform previewRoot = previewRootObject.transform;
        previewRoot.SetParent(grid.transform, false);

        var tiles = CollectSceneTiles(grid, previewRoot);
        AddMissingExpansionTiles(grid, grassPrefab, previewRoot, tiles);
        AddTileDecorations(grid, serializedGrid, previewRoot, tiles);
        AddPersistentDecorationGround(grid, grassPrefab, previewRoot);
        AddFinalFarmFence(grid, previewRoot);

        SetPreviewFlagsRecursively(previewRootObject);

    }

    private static Dictionary<Vector2Int, TilePreviewInfo> CollectSceneTiles(
        GridManager grid,
        Transform previewRoot)
    {
        var tiles = new Dictionary<Vector2Int, TilePreviewInfo>();
        foreach (Transform child in grid.transform)
        {
            if (child == previewRoot)
                continue;

            TileMarker marker = child.GetComponent<TileMarker>();
            if (marker == null || !grid.WorldToGrid(child.position, out int x, out int z))
                continue;

            tiles[new Vector2Int(x, z)] = new TilePreviewInfo(child.position, marker.tileType);
        }

        return tiles;
    }

    private static void AddMissingExpansionTiles(
        GridManager grid,
        GameObject grassPrefab,
        Transform previewRoot,
        IDictionary<Vector2Int, TilePreviewInfo> tiles)
    {
        // Startflaeche absichern und danach exakt dieselben fuenf Rechtecke benutzen,
        // aus denen ZoneManager im Play Mode Kaufzonen und Sperren erzeugt.
        AddMissingTilesInRect(
            new RectInt(0, 0, grid.BaseWidth, grid.BaseHeight),
            grid,
            grassPrefab,
            previewRoot,
            tiles);

        foreach (FarmExpansionArea area in ZoneManager.CreateDirectionalLayout(grid))
            AddMissingTilesInRect(area.Tiles, grid, grassPrefab, previewRoot, tiles);
    }

    private static void AddMissingTilesInRect(
        RectInt rect,
        GridManager grid,
        GameObject grassPrefab,
        Transform previewRoot,
        IDictionary<Vector2Int, TilePreviewInfo> tiles)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int z = rect.yMin; z < rect.yMax; z++)
            {
                var coordinate = new Vector2Int(x, z);
                if (tiles.ContainsKey(coordinate))
                    continue;

                Vector3 position = grid.GridToWorld(x, z);
                GameObject tile = InstantiatePreview(
                    grassPrefab,
                    position,
                    Quaternion.identity,
                    previewRoot,
                    $"Preview Grass {x},{z}");
                DisableColliders(tile);
                tiles.Add(coordinate, new TilePreviewInfo(position, TileType.Grass));
            }
        }
    }

    private static void AddTileDecorations(
        GridManager grid,
        SerializedObject serializedGrid,
        Transform previewRoot,
        IReadOnlyDictionary<Vector2Int, TilePreviewInfo> tiles)
    {
        bool grassEnabled = ReadBool(serializedGrid, "enableGrassTileDecoration");
        GameObject[] grassPrefabs = ReadObjectArray<GameObject>(serializedGrid, "grassPlantDecorationPrefabs");
        int grassSeed = ReadInt(serializedGrid, "grassDecorationSeed");
        float grassOffset = ReadFloat(serializedGrid, "grassDecorationSurfaceOffset");
        float grassMinScale = ReadFloat(serializedGrid, "grassPlantDecorationMinScale");
        float grassMaxScale = ReadFloat(serializedGrid, "grassPlantDecorationMaxScale");
        float grassChance = ReadFloat(serializedGrid, "grassTileDecorationChance");
        float secondGrassChance = ReadFloat(serializedGrid, "secondGrassDecorationChance");
        Rect[] blockedAreas = ReadRectArray(serializedGrid, "grassDecorationBlockedAreas");

        bool pathEnabled = ReadBool(serializedGrid, "enablePathTileDecoration");
        GameObject[] pathPrefabs = ReadObjectArray<GameObject>(serializedGrid, "pathStoneDecorationPrefabs");
        int pathSeed = ReadInt(serializedGrid, "pathDecorationSeed");
        float pathOffset = ReadFloat(serializedGrid, "pathDecorationSurfaceOffset");
        float pathMinScale = ReadFloat(serializedGrid, "pathStoneDecorationMinScale");
        float pathMaxScale = ReadFloat(serializedGrid, "pathStoneDecorationMaxScale");
        float pathChance = ReadFloat(serializedGrid, "pathTileDecorationChance");
        float secondPathChance = ReadFloat(serializedGrid, "secondPathStoneChance");

        foreach (KeyValuePair<Vector2Int, TilePreviewInfo> pair in tiles)
        {
            Vector2Int coordinate = pair.Key;
            TilePreviewInfo tile = pair.Value;

            if (tile.Type == TileType.Grass && grassEnabled &&
                grassPrefabs.Length > 0 && !IsBlocked(tile.Position, blockedAreas))
            {
                int seed = HashCoordinate(grassSeed, coordinate.x, coordinate.y);
                AddDeterministicDecoration(
                    previewRoot,
                    tile.Position,
                    grid.CellSize,
                    grassPrefabs,
                    seed,
                    grassChance,
                    secondGrassChance,
                    grassOffset,
                    grassMinScale,
                    grassMaxScale,
                    0.1f,
                    0.34f,
                    0.3f,
                    "Preview Grass Misc");
            }
            else if (tile.Type == TileType.Path && pathEnabled && pathPrefabs.Length > 0)
            {
                int seed = HashCoordinate(pathSeed, coordinate.x, coordinate.y);
                AddDeterministicDecoration(
                    previewRoot,
                    tile.Position,
                    grid.CellSize,
                    pathPrefabs,
                    seed,
                    pathChance,
                    secondPathChance,
                    pathOffset,
                    pathMinScale,
                    pathMaxScale,
                    0.12f,
                    0.35f,
                    0.35f,
                    "Preview Path Stone");
            }
        }
    }

    private static void AddDeterministicDecoration(
        Transform parent,
        Vector3 tilePosition,
        float cellSize,
        GameObject[] prefabs,
        int seed,
        float spawnChance,
        float secondObjectChance,
        float surfaceOffset,
        float minScale,
        float maxScale,
        float minRadiusFactor,
        float maxRadiusFactor,
        float angleJitter,
        string namePrefix)
    {
        var random = new System.Random(seed);
        if (random.NextDouble() >= spawnChance)
            return;

        int count = random.NextDouble() < secondObjectChance ? 2 : 1;
        float baseAngle = NextFloat(random, 0f, Mathf.PI * 2f);
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = PickPrefab(prefabs, random);
            if (prefab == null)
                continue;

            float angle = baseAngle + Mathf.PI * 2f * i / count +
                          NextFloat(random, -angleJitter, angleJitter);
            float radius = NextFloat(random, cellSize * minRadiusFactor, cellSize * maxRadiusFactor);
            float scale = NextFloat(random, Mathf.Min(minScale, maxScale), Mathf.Max(minScale, maxScale));
            Vector3 position = tilePosition + new Vector3(
                Mathf.Cos(angle) * radius,
                surfaceOffset,
                Mathf.Sin(angle) * radius);

            GameObject decoration = InstantiatePreview(
                prefab,
                position,
                Quaternion.Euler(0f, NextFloat(random, 0f, 360f), 0f),
                parent,
                $"{namePrefix} {i + 1}");
            decoration.transform.localScale = prefab.transform.localScale * scale;
            DisableColliders(decoration);
            DisableShadows(decoration);
        }
    }

    private static void AddPersistentDecorationGround(
        GridManager grid,
        GameObject grassPrefab,
        Transform previewRoot)
    {
        Renderer grassRenderer = grassPrefab.GetComponentInChildren<Renderer>();
        Material grassMaterial = grassRenderer != null ? grassRenderer.sharedMaterial : null;
        float cellSize = grid.CellSize;

        float environmentMinX = grid.EnvironmentMinX * cellSize;
        float environmentMaxX = grid.EnvironmentMaxXExclusive * cellSize;
        float environmentMinZ = grid.EnvironmentMinZ * cellSize;
        float environmentMaxZ = grid.EnvironmentMaxZExclusive * cellSize;
        float farmMinX = grid.FarmMinX * cellSize;
        float farmMaxX = grid.FarmMaxXExclusive * cellSize;
        float farmMinZ = grid.FarmMinZ * cellSize;
        float farmMaxZ = grid.FarmMaxZExclusive * cellSize;

        AddGroundSection("Preview Ground Left", environmentMinX, farmMinX,
            environmentMinZ, environmentMaxZ, previewRoot, grassMaterial);
        AddGroundSection("Preview Ground Right", farmMaxX, environmentMaxX,
            environmentMinZ, environmentMaxZ, previewRoot, grassMaterial);
        AddGroundSection("Preview Ground Bottom", farmMinX, farmMaxX,
            environmentMinZ, farmMinZ, previewRoot, grassMaterial);
        AddGroundSection("Preview Ground Top", farmMinX, farmMaxX,
            farmMaxZ, environmentMaxZ, previewRoot, grassMaterial);
    }

    private static void AddGroundSection(
        string name,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        Transform parent,
        Material material)
    {
        float width = maxX - minX;
        float depth = maxZ - minZ;
        if (width <= 0f || depth <= 0f)
            return;

        GameObject section = GameObject.CreatePrimitive(PrimitiveType.Cube);
        section.name = name;
        section.transform.SetParent(parent, false);
        section.transform.localPosition = new Vector3(
            (minX + maxX) * 0.5f,
            0f,
            (minZ + maxZ) * 0.5f);
        section.transform.localScale = new Vector3(width, 0.1f, depth);

        DisableColliders(section);
        DisableShadows(section);
        Renderer renderer = section.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
    }

    /// <summary>
    /// Die Scene View zeigt bewusst den voll ausgebauten Endzustand: keine Kaufbaeume
    /// und kein alter Startzaun, sondern der Zaun um die gemeinsame Endflaeche.
    /// </summary>
    private static void AddFinalFarmFence(GridManager grid, Transform previewRoot)
    {
        ZoneManager zoneManager = FindZoneManager(grid.gameObject.scene);
        if (zoneManager == null)
            return;

        var serializedZones = new SerializedObject(zoneManager);
        if (!ReadBool(serializedZones, "useDirectionalExpansions"))
            return;

        string fenceRootName = ReadString(serializedZones, "existingFenceRootName");
        Transform oldFenceRoot = FindSceneTransform(grid.gameObject.scene, fenceRootName);
        if (oldFenceRoot == null || oldFenceRoot.childCount == 0)
            return;

        GameObject template = oldFenceRoot.GetChild(0).gameObject;
        float segmentLength = Mathf.Max(0.1f, ReadFloat(serializedZones, "fenceSegmentLength"));
        Vector3 visualOffset = ReadVector3(serializedZones, "fenceVisualOffset");
        Vector3 templateScale = template.transform.lossyScale;

        // Dieselben alten Zaunobjekte ausblenden, die ZoneManager im Play Mode deaktiviert.
        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != grid.gameObject.scene)
                continue;
            if (candidate == oldFenceRoot || candidate.name.StartsWith("Env_WoodFence_02"))
                HideForPreview(candidate.gameObject);
        }

        int farmWidth = grid.FarmMaxXExclusive - grid.FarmMinX;
        int farmHeight = grid.FarmMaxZExclusive - grid.FarmMinZ;
        SpawnPreviewFenceRun(grid, template, templateScale, segmentLength, visualOffset,
            grid.FarmMinZ, grid.FarmMinX, farmWidth, true, previewRoot);
        SpawnPreviewFenceRun(grid, template, templateScale, segmentLength, visualOffset,
            grid.FarmMaxZExclusive, grid.FarmMinX, farmWidth, true, previewRoot);
        SpawnPreviewFenceRun(grid, template, templateScale, segmentLength, visualOffset,
            grid.FarmMinX, grid.FarmMinZ, farmHeight, false, previewRoot);
        SpawnPreviewFenceRun(grid, template, templateScale, segmentLength, visualOffset,
            grid.FarmMaxXExclusive, grid.FarmMinZ, farmHeight, false, previewRoot);
    }

    private static void SpawnPreviewFenceRun(
        GridManager grid,
        GameObject template,
        Vector3 templateScale,
        float segmentLength,
        Vector3 visualOffset,
        int boundary,
        int runStart,
        int runLength,
        bool horizontal,
        Transform parent)
    {
        int segmentCells = Mathf.Max(1, Mathf.RoundToInt(segmentLength / grid.CellSize));
        for (int offset = 0; offset < runLength; offset += segmentCells)
        {
            int cellsInPiece = Mathf.Min(segmentCells, runLength - offset);
            int pieceStart = runStart + offset;
            float pieceLength = cellsInPiece * grid.CellSize;
            float alongCenter = pieceStart * grid.CellSize + pieceLength * 0.5f;
            Vector3 position = grid.transform.position + (horizontal
                ? new Vector3(alongCenter, 0f, boundary * grid.CellSize)
                : new Vector3(boundary * grid.CellSize, 0f, alongCenter));
            position += visualOffset;

            GameObject piece = InstantiatePreview(
                template,
                position,
                Quaternion.Euler(0f, horizontal ? 0f : -90f, 0f),
                parent,
                horizontal ? "Preview Farm Fence Horizontal" : "Preview Farm Fence Vertical");
            piece.transform.localScale = Vector3.Scale(
                templateScale,
                new Vector3(pieceLength / segmentLength, 1f, 1f));
            piece.SetActive(true);
            DisableColliders(piece);
        }
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == scene && candidate.name == objectName)
                return candidate;
        }

        return null;
    }

    private static void HideForPreview(GameObject target)
    {
        if (target == null || SceneVisibilityManager.instance.IsHidden(target))
            return;

        SceneVisibilityManager.instance.Hide(target, true);
        HiddenForPreview.Add(target);
    }

    private static void RestoreSceneVisibility()
    {
        foreach (GameObject target in HiddenForPreview)
        {
            if (target != null)
                SceneVisibilityManager.instance.Show(target, true);
        }

        HiddenForPreview.Clear();
    }

    private static ZoneManager FindZoneManager(Scene scene)
    {
        foreach (ZoneManager manager in UnityEngine.Object.FindObjectsByType<ZoneManager>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (manager.gameObject.scene == scene)
                return manager;
        }

        return null;
    }

    private static void ClearPreview(GridManager grid)
    {
        if (grid == null)
            return;

        Transform existing = grid.transform.Find(PreviewRootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
    }

    private static GameObject InstantiatePreview(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        string instanceName)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
        instance.name = instanceName;
        SetPreviewFlagsRecursively(instance);
        return instance;
    }

    private static void SetPreviewFlagsRecursively(GameObject root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = PreviewFlags;
    }

    private static void DisableColliders(GameObject root)
    {
        if (root == null)
            return;

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    private static void DisableShadows(GameObject root)
    {
        if (root == null)
            return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static bool IsBlocked(Vector3 worldPosition, Rect[] blockedAreas)
    {
        Vector2 point = new(worldPosition.x, worldPosition.z);
        foreach (Rect area in blockedAreas)
        {
            if (area.Contains(point))
                return true;
        }

        return false;
    }

    private static int HashCoordinate(int seed, int x, int z)
    {
        unchecked
        {
            int hash = seed;
            hash = hash * 397 ^ x;
            hash = hash * 397 ^ z;
            return hash;
        }
    }

    private static GameObject PickPrefab(GameObject[] prefabs, System.Random random)
    {
        for (int attempt = 0; attempt < prefabs.Length; attempt++)
        {
            GameObject prefab = prefabs[random.Next(prefabs.Length)];
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    private static float NextFloat(System.Random random, float min, float max) =>
        min + (max - min) * (float)random.NextDouble();

    private static T ReadObject<T>(SerializedObject serializedObject, string propertyName)
        where T : UnityEngine.Object =>
        serializedObject.FindProperty(propertyName)?.objectReferenceValue as T;

    private static bool ReadBool(SerializedObject serializedObject, string propertyName) =>
        serializedObject.FindProperty(propertyName)?.boolValue ?? false;

    private static int ReadInt(SerializedObject serializedObject, string propertyName) =>
        serializedObject.FindProperty(propertyName)?.intValue ?? 0;

    private static float ReadFloat(SerializedObject serializedObject, string propertyName) =>
        serializedObject.FindProperty(propertyName)?.floatValue ?? 0f;

    private static string ReadString(SerializedObject serializedObject, string propertyName) =>
        serializedObject.FindProperty(propertyName)?.stringValue ?? string.Empty;

    private static Vector3 ReadVector3(SerializedObject serializedObject, string propertyName) =>
        serializedObject.FindProperty(propertyName)?.vector3Value ?? Vector3.zero;

    private static T[] ReadObjectArray<T>(SerializedObject serializedObject, string propertyName)
        where T : UnityEngine.Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return Array.Empty<T>();

        var result = new T[property.arraySize];
        for (int i = 0; i < result.Length; i++)
            result[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as T;

        return result;
    }

    private static Rect[] ReadRectArray(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return Array.Empty<Rect>();

        var result = new Rect[property.arraySize];
        for (int i = 0; i < result.Length; i++)
            result[i] = property.GetArrayElementAtIndex(i).rectValue;

        return result;
    }

    private readonly struct TilePreviewInfo
    {
        public Vector3 Position { get; }
        public TileType Type { get; }

        public TilePreviewInfo(Vector3 position, TileType type)
        {
            Position = position;
            Type = type;
        }
    }

}

[CustomEditor(typeof(GridManager))]
public sealed class GridManagerPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
            FarmSceneEditorPreview.QueueRefresh();

        EditorGUILayout.Space();
        if (GUILayout.Button("Farm-Vorschau aktualisieren"))
            FarmSceneEditorPreview.RefreshNow();
        if (GUILayout.Button("Farm-Vorschau ausblenden"))
            FarmSceneEditorPreview.ClearAllPreviews();
    }
}
#endif
