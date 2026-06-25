using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zentrale Liste aller PlantType-Assets.
///
/// Wichtig:
/// - Am besten auf dasselbe GameObject wie FarmSaveManager legen: "SaveSystem" in der MainMenu-Scene.
/// - Alle PlantType-Assets im Inspector in "All Plant Types" eintragen.
/// - Das Objekt bleibt standardmäßig über Szenen hinweg erhalten, damit Laden aus MainMenu -> GameScene funktioniert.
/// </summary>
public class PlantDatabase : MonoBehaviour
{
    public static PlantDatabase Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private List<PlantType> allPlantTypes = new();
    public IReadOnlyList<PlantType> AllPlantTypes => allPlantTypes;

    [Header("Lifetime")]
    [Tooltip("Empfohlen anlassen, wenn PlantDatabase auf dem SaveSystem im MainMenu liegt.")]
    [SerializeField] private bool persistAcrossScenes = true;

    private readonly Dictionary<string, PlantType> byId = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("[PlantDatabase] Duplicate PlantDatabase gefunden. Die bereits aktive Database wird weiter benutzt.");
            Destroy(this);
            return;
        }

        Instance = this;
        RebuildLookup();

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && Instance == this)
            RebuildLookup();
    }
#endif

    public void RebuildLookup()
    {
        byId.Clear();

        foreach (PlantType plantType in allPlantTypes)
        {
            if (plantType == null) continue;

            string id = GetPlantId(plantType);
            if (string.IsNullOrEmpty(id)) continue;

            if (byId.ContainsKey(id))
            {
                Debug.LogWarning($"[PlantDatabase] Doppelte Plant SaveId '{id}'. Bitte eindeutige IDs vergeben.");
                continue;
            }

            byId.Add(id, plantType);
        }

        Debug.Log($"[PlantDatabase] Geladen: {byId.Count} PlantType(s).");
    }

    public PlantType GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        byId.TryGetValue(id, out PlantType plantType);
        return plantType;
    }

    public static string GetPlantId(PlantType plantType)
    {
        if (plantType == null) return string.Empty;
        return plantType.SaveId;
    }
}
