using UnityEngine;

public enum UiSfxKind
{
    Click,
    Back,
    Open,
    Close,
    Purchase,
    Denied,

    /// <summary>Gar kein Sound. Für Buttons, die ihren Klang selbst auslösen.</summary>
    Silent
}

/// <summary>
/// Legt fest, wie ein einzelner Button klingt. Ohne diese Komponente bekommt jeder Button
/// den Standard-Klick.
///
/// Gedacht für die Handvoll Buttons, bei denen der Standardklick falsch wäre: "Schließen"
/// soll nach Schließen klingen, "Kaufen" nach Kauf. Der Rest bleibt unangetastet — sonst
/// hängt man am Ende an dreißig Buttons Komponenten und hat nichts gewonnen.
///
/// Setup: Button anwählen → Add Component → UI Sfx Override → Art auswählen.
/// </summary>
[DisallowMultipleComponent]
public class UiSfxOverride : MonoBehaviour
{
    [Tooltip("Welcher Klang beim Klick kommt.")]
    public UiSfxKind kind = UiSfxKind.Click;
}
