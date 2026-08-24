using UnityEngine;

/// <summary>
/// Erstellt kleine, kamerazugewandte Statussymbole über allen Markt-Händlern.
/// Verwendet bewusst dasselbe Prefab und denselben Shader wie die Pflanzenanzeigen,
/// damit beide Systeme dieselbe visuelle Sprache sprechen.
/// </summary>
[DisallowMultipleComponent]
public class FarmMarketNpcStatusIcons : MonoBehaviour
{
    [Header("Referenz")]
    [Tooltip("Dasselbe Prefab wie beim PlantStatusOverlay: 'Plant Status'.")]
    [SerializeField] private GameObject iconPrefab;

    [Header("Anzeige")]
    [SerializeField, Min(0f)] private float heightOffset = 0.32f;
    [SerializeField, Min(0.05f)] private float iconWorldSize = 0.55f;

    [Header("Farben")]
    [SerializeField] private Color seedColor = new(0.55f, 0.88f, 0.38f, 1f);
    [SerializeField] private Color sellColor = new(1f, 0.78f, 0.25f, 1f);
    [SerializeField] private Color toolColor = new(0.95f, 0.52f, 0.24f, 1f);
    [SerializeField] private Color licenseColor = new(0.52f, 0.72f, 1f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int SymbolId = Shader.PropertyToID("_Symbol");
    private static readonly int ScaleId = Shader.PropertyToID("_Scale");

    // Muss zu den Symbolwerten im PlantStatus-Shader passen.
    private const float SeedSymbol = 3f;
    private const float SellSymbol = 4f;
    private const float ToolSymbol = 5f;
    private const float LicenseSymbol = 6f;

    private void Start()
    {
        if (iconPrefab == null)
        {
            Debug.LogWarning($"[{nameof(FarmMarketNpcStatusIcons)}] Icon-Prefab fehlt.", this);
            return;
        }

        foreach (FarmMarketNpc npc in GetComponentsInChildren<FarmMarketNpc>(true))
            CreateIcon(npc);
    }

    private void CreateIcon(FarmMarketNpc npc)
    {
        if (npc == null || npc.transform.Find("Market NPC Status Icon") != null)
            return;

        float topY = ResolveRendererTop(npc);
        GameObject icon = Instantiate(iconPrefab, npc.transform);
        icon.name = "Market NPC Status Icon";
        icon.transform.position = new Vector3(
            npc.transform.position.x,
            topY + heightOffset,
            npc.transform.position.z);

        Renderer iconRenderer = icon.GetComponentInChildren<Renderer>();
        if (iconRenderer == null)
        {
            Destroy(icon);
            return;
        }

        var properties = new MaterialPropertyBlock();
        iconRenderer.GetPropertyBlock(properties);
        properties.SetColor(BaseColorId, ResolveColor(npc.TradeMode));
        properties.SetFloat(ProgressId, 1f);
        properties.SetFloat(SymbolId, ResolveSymbol(npc.TradeMode));
        properties.SetFloat(ScaleId, iconWorldSize);
        iconRenderer.SetPropertyBlock(properties);
    }

    private static float ResolveRendererTop(FarmMarketNpc npc)
    {
        float topY = npc.transform.position.y + 2f;

        foreach (Renderer renderer in npc.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled)
                continue;

            topY = Mathf.Max(topY, renderer.bounds.max.y);
        }

        return topY;
    }

    private static float ResolveSymbol(FarmMarketNpcTradeMode mode) => mode switch
    {
        FarmMarketNpcTradeMode.BuySeeds => SeedSymbol,
        FarmMarketNpcTradeMode.SellInventory => SellSymbol,
        FarmMarketNpcTradeMode.ToolUpgrade => ToolSymbol,
        FarmMarketNpcTradeMode.Licenses => LicenseSymbol,
        _ => SeedSymbol
    };

    private Color ResolveColor(FarmMarketNpcTradeMode mode) => mode switch
    {
        FarmMarketNpcTradeMode.BuySeeds => seedColor,
        FarmMarketNpcTradeMode.SellInventory => sellColor,
        FarmMarketNpcTradeMode.ToolUpgrade => toolColor,
        FarmMarketNpcTradeMode.Licenses => licenseColor,
        _ => Color.white
    };
}
