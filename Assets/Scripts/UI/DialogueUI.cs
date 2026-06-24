using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dialogue-Panel. Singleton + DontDestroyOnLoad — überlebt Scenewechsel.
/// Das Canvas auf dem dieses Script sitzt muss ein eigenes Root-GO sein (kein Parent).
/// Setup: Panel GO + SpeakerName TMP + DialogueText TMP + Next Button + optional Portrait Image.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Image portrait;
    [SerializeField] private Button nextButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Root-Canvas (PersistentUI) szenenübergreifend erhalten
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted += OnDialogueStarted;
            DialogueManager.Instance.OnLineChanged += OnLineChanged;
            DialogueManager.Instance.OnDialogueEnded += OnDialogueEnded;

            // Falls Dialogue schon aktiv ist wenn UI in die Scene kommt (Scene-Reload)
            if (DialogueManager.Instance.IsActive)
            {
                OnDialogueStarted();
                OnLineChanged(DialogueManager.Instance.CurrentLine);
            }
        }
    }

    private void OnDestroy()
    {
        if (DialogueManager.Instance == null) return;
        DialogueManager.Instance.OnDialogueStarted -= OnDialogueStarted;
        DialogueManager.Instance.OnLineChanged -= OnLineChanged;
        DialogueManager.Instance.OnDialogueEnded -= OnDialogueEnded;
    }

    private void OnDialogueStarted()
    {
        if (panel != null) panel.SetActive(true);
    }

    private void OnLineChanged(DialogueLine line)
    {
        if (line == null) return;

        if (speakerNameText != null) speakerNameText.text = line.speakerName;
        if (dialogueText != null) dialogueText.text = line.text;

        if (portrait != null)
        {
            portrait.gameObject.SetActive(line.portrait != null);
            if (line.portrait != null) portrait.sprite = line.portrait;
        }
    }

    private void OnDialogueEnded()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnNextClicked()
    {
        DialogueManager.Instance?.NextLine();
    }
}
