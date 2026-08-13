using System.Collections.Generic;
using UnityEngine;

public enum FarmMarketNpcTradeMode
{
    BuySeeds,
    SellInventory,
    ToolUpgrade,

    /// <summary>Lizenz-Amt: verkauft die großen Freischaltungen.</summary>
    Licenses
}

/// <summary>
/// Früher: Samen der erst ab einem Story-Fortschritt verkauft wurde.
///
/// Das Gating läuft inzwischen ausschließlich über <see cref="LicenseData"/> — zwei Systeme
/// für dieselbe Sache waren eine Fehlerquelle. Die Klasse bleibt nur stehen, damit Unity
/// die alten <c>gatedSeeds</c>-Einträge in den Szenen beim Import nicht als Fehler meldet;
/// sie wird nirgends mehr gelesen und kann weg, sobald die Szenen einmal neu gespeichert sind.
/// </summary>
[System.Serializable]
public class GatedSeed
{
    public PlantType plant;
    public string requiredMissionId;
}

/// <summary>
/// Dieses Script kommt auf jeden Markt-NPC/Cube.
/// TradeMode entscheidet, welches UI ge�ffnet wird:
/// BuySeeds      -> BuyPanel
/// SellInventory -> SellPanel
/// </summary>
[RequireComponent(typeof(Collider))]
public class FarmMarketNpc : MonoBehaviour
{
    [Header("NPC")]
    [SerializeField] private string displayName = "Merchant";

    [TextArea]
    [SerializeField] private string speechText = "Hey! Want to trade?";

    [Header("Trade Type")]
    [SerializeField] private FarmMarketNpcTradeMode tradeMode = FarmMarketNpcTradeMode.BuySeeds;

    [Header("Nur f�r BuySeeds-NPC")]
    [SerializeField] private List<PlantType> seedsForSale = new List<PlantType>();

    [Header("Optional Camera Focus")]
    [SerializeField] private Transform cameraFocusPoint;

    [Header("Movement")]
    [SerializeField] private bool addPatrolIfMissing = true;

    private FarmMarketNpcPatrol patrol;

    public string DisplayName => displayName;
    public string SpeechText => speechText;
    public FarmMarketNpcTradeMode TradeMode => tradeMode;
    /// <summary>
    /// Das gesamte Sortiment — auch das, wofür noch die Lizenz fehlt.
    ///
    /// Bewusst ohne Filter: der Händler zeigt immer alles, gesperrte Sorten stellt der Shop
    /// ausgegraut mit "Lizenz nötig" dar. Ein leeres Regal verrät dem Spieler nicht, dass es
    /// überhaupt mehr gibt — sichtbare, aber gesperrte Ware ist der eigentliche Kaufanreiz.
    /// Ob eine Sorte kaufbar ist, entscheidet allein <see cref="LicenseRegistry"/>.
    /// </summary>
    public IReadOnlyList<PlantType> SeedsForSale
    {
        get
        {
            currentOffer.Clear();

            foreach (var plant in seedsForSale)
                if (plant != null && !currentOffer.Contains(plant))
                    currentOffer.Add(plant);

            return currentOffer;
        }
    }

    private readonly List<PlantType> currentOffer = new List<PlantType>();

    public Transform CameraFocusPoint => cameraFocusPoint != null ? cameraFocusPoint : transform;

    private void Awake()
    {
        patrol = GetComponent<FarmMarketNpcPatrol>();

        if (patrol == null && addPatrolIfMissing)
            patrol = gameObject.AddComponent<FarmMarketNpcPatrol>();
    }

    public void BeginDialogue()
    {
        if (patrol == null)
            patrol = GetComponent<FarmMarketNpcPatrol>();

        if (patrol != null)
            patrol.EnterDialogue();
    }

    public void EndDialogue()
    {
        if (patrol == null)
            patrol = GetComponent<FarmMarketNpcPatrol>();

        if (patrol != null)
            patrol.ExitDialogue();
    }
}
