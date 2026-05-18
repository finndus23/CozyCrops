using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Optionales Testscript.
/// In der aktuellen Version verarbeitet FarmSaveManager F5/F6 bereits selbst.
/// Dieses Script bleibt nur als Fallback vorhanden, falls du die Hotkeys im FarmSaveManager deaktivierst.
/// </summary>
public class SaveHotkeys : MonoBehaviour
{
    private void Update()
    {
        if (FarmSaveManager.Instance == null) return;

        // Wenn FarmSaveManager die Hotkeys selbst verarbeitet, hier nichts doppelt auslösen.
        if (FarmSaveManager.Instance.HandlesDebugHotkeys) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f5Key.wasPressedThisFrame)
        {
            FarmSaveManager.Instance.SaveNow();
            Debug.Log($"[SaveHotkeys] Save-Datei liegt hier: {FarmSaveManager.Instance.CurrentSavePath}");
        }

        if (keyboard.f6Key.wasPressedThisFrame)
        {
            bool loadStarted = FarmSaveManager.Instance.LoadNow();
            Debug.Log(loadStarted
                ? $"[SaveHotkeys] Load gestartet: {FarmSaveManager.Instance.CurrentSavePath}"
                : $"[SaveHotkeys] Save-Datei konnte nicht geladen werden: {FarmSaveManager.Instance.CurrentSavePath}");
        }
    }
}
