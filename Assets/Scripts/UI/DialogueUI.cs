using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dialogue panel. Singleton + DontDestroyOnLoad, survives scene changes.
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
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite nextButtonSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        ApplyPanelStyle();
    }

    private void Start()
    {
        ApplyPanelStyle();

        if (panel != null) panel.SetActive(false);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted += OnDialogueStarted;
            DialogueManager.Instance.OnLineChanged += OnLineChanged;
            DialogueManager.Instance.OnDialogueEnded += OnDialogueEnded;

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

    private void ApplyPanelStyle()
    {
        if (panel == null)
            panel = gameObject;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 26f);
            panelRect.sizeDelta = new Vector2(1040f, 347f);
        }

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = panelSprite;
            panelImage.color = Color.white;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = false;
        }

        RectTransform portraitRect = portrait != null ? portrait.GetComponent<RectTransform>() : null;
        if (portraitRect != null)
        {
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(154f, 42f);
            portraitRect.sizeDelta = new Vector2(166f, 166f);
        }

        if (portrait != null)
        {
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
        }

        RectTransform speakerRect = speakerNameText != null ? speakerNameText.GetComponent<RectTransform>() : null;
        if (speakerRect != null)
        {
            speakerRect.anchorMin = new Vector2(0f, 0.5f);
            speakerRect.anchorMax = new Vector2(0f, 0.5f);
            speakerRect.pivot = new Vector2(0.5f, 0.5f);
            speakerRect.anchoredPosition = new Vector2(154f, -68f);
            speakerRect.sizeDelta = new Vector2(205f, 34f);
        }

        if (speakerNameText != null)
        {
            speakerNameText.alignment = TextAlignmentOptions.Center;
            speakerNameText.fontSize = 40f;
            speakerNameText.enableAutoSizing = true;
            speakerNameText.fontSizeMin = 13f;
            speakerNameText.fontSizeMax = 20f;
            speakerNameText.color = new Color(0.22f, 0.12f, 0.04f, 1f);
            speakerNameText.raycastTarget = false;
        }

        RectTransform textRect = dialogueText != null ? dialogueText.GetComponent<RectTransform>() : null;
        if (textRect != null)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(332f, 95f);
            textRect.offsetMax = new Vector2(-120f, -94f);
        }

        if (dialogueText != null)
        {
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            dialogueText.fontSize = 26f;
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = 18f;
            dialogueText.fontSizeMax = 26f;
            dialogueText.color = new Color(0.22f, 0.12f, 0.04f, 1f);
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.overflowMode = TextOverflowModes.Ellipsis;
            dialogueText.raycastTarget = false;
        }

        RectTransform buttonRect = nextButton != null ? nextButton.GetComponent<RectTransform>() : null;
        if (buttonRect != null)
        {
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-50f, 50f);
            buttonRect.sizeDelta = new Vector2(180f, 100f);
        }

        if (nextButton != null)
        {
            nextButton.transition = Selectable.Transition.None;

            Image buttonImage = nextButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.sprite = nextButtonSprite;
                buttonImage.color = nextButtonSprite != null ? Color.white : Color.clear;
                buttonImage.type = Image.Type.Simple;
                buttonImage.preserveAspect = false;
            }

            TextMeshProUGUI[] tmpTexts = nextButton.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in tmpTexts)
                text.color = Color.clear;

            Text[] legacyTexts = nextButton.GetComponentsInChildren<Text>(true);
            foreach (Text text in legacyTexts)
                text.color = Color.clear;
        }
    }
}
