using System.Collections;
using UnityEngine;

/// <summary>
/// In die MarketScene legen.
/// Lädt beim Start nur Geld/Inventar aus dem aktiven Save-Slot.
/// Grid/Farm-Tiles werden im Markt bewusst nicht geladen.
/// </summary>
public class FarmMarketSceneInventoryBootstrap : MonoBehaviour
{
    [SerializeField] private bool loadInventoryOnStart = true;
    [SerializeField] private int framesToWaitBeforeLoad = 2;

    private IEnumerator Start()
    {
        for (int i = 0; i < framesToWaitBeforeLoad; i++)
            yield return null;

        if (!loadInventoryOnStart)
            yield break;

        if (FarmSaveManager.Instance == null)
        {
            Debug.LogWarning("[FarmMarketSceneInventoryBootstrap] Kein FarmSaveManager gefunden. Starte die MarketScene am besten über MainMenu -> Slot -> Farm -> Market, oder lege SaveSystem mit FarmSaveManager in die erste Scene.");
            yield break;
        }

        if (PlayerInventory.Instance == null)
        {
            Debug.LogWarning("[FarmMarketSceneInventoryBootstrap] Kein PlayerInventory in der MarketScene gefunden. Lege ein GameObject 'PlayerInventory' mit PlayerInventory-Script in die MarketScene.");
            yield break;
        }

        FarmSaveManager.Instance.LoadInventoryOnlyNow();
    }
}
