using System;
using UnityEngine;

/// <summary>
/// Steuert den aktuellen Dialog. Sitzt auf dem SaveSystem-Objekt (DontDestroyOnLoad).
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    private DialogueData currentDialogue;
    private int currentLineIndex;
    private bool isActive;

    public event Action OnDialogueStarted;
    public event Action<DialogueLine> OnLineChanged;
    public event Action OnDialogueEnded;

    public bool IsActive => isActive;

    public DialogueLine CurrentLine =>
        currentDialogue != null && currentLineIndex < currentDialogue.lines.Length
            ? currentDialogue.lines[currentLineIndex]
            : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartDialogue(DialogueData data)
    {
        if (data == null || data.lines == null || data.lines.Length == 0) return;

        currentDialogue = data;
        currentLineIndex = 0;
        isActive = true;

        OnDialogueStarted?.Invoke();
        OnLineChanged?.Invoke(CurrentLine);
    }

    public void NextLine()
    {
        if (!isActive || currentDialogue == null) return;

        currentLineIndex++;

        if (currentLineIndex >= currentDialogue.lines.Length)
        {
            EndDialogue();
            return;
        }

        OnLineChanged?.Invoke(CurrentLine);
    }

    /// <summary>Dialog sofort beenden (z.B. wenn Tutorial-Objective während Dialog erfüllt wird).</summary>
    public void ForceEnd()
    {
        if (!isActive) return;
        isActive = false;
        currentDialogue = null;
        OnDialogueEnded?.Invoke();
    }

    private void EndDialogue()
    {
        isActive = false;
        var finished = currentDialogue;
        currentDialogue = null;

        OnDialogueEnded?.Invoke();

        // Mission starten die mit diesem Dialog verknüpft ist
        if (finished?.missionToStartAfter != null)
            MissionManager.Instance?.StartMission(finished.missionToStartAfter);
    }
}
