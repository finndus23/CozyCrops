using System.Collections.Generic;
using UnityEngine;

public enum FarmMarketNpcTradeMode
{
    BuySeeds,
    SellInventory,
    ToolUpgrade
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
    public IReadOnlyList<PlantType> SeedsForSale => seedsForSale;
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
