using System.Collections;
using UnityEngine;

public class FarmSceneAutoLoad : MonoBehaviour
{
    [Header("Load Timing")]
    [SerializeField] private int framesToWaitBeforeLoad = 2;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private IEnumerator Start()
    {
        for (int i = 0; i < framesToWaitBeforeLoad; i++)
            yield return null;

        if (FarmSaveManager.Instance == null)
        {
            Debug.LogWarning("[FarmSceneAutoLoad] Kein FarmSaveManager gefunden. Farm kann nicht geladen werden.");
            yield break;
        }

        if (logDebug)
            Debug.Log("[FarmSceneAutoLoad] Lade aktiven Slot für FarmScene...");

        FarmSaveManager.Instance.LoadNow();
    }
}