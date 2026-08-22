using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Schwebender Text für drei "kleiner Glücksfund"-Momente:
///  • Sichel im Gras findet einen Not-Samen (PlantManager.OnStarterSeedFound)
///  • Ernte eines gedüngten Feldes würfelt den Dünger-Bonus (PlantManager.OnFertilizedHarvestBonus)
///  • JEDE Ernte würfelt den allgemeinen Sichel-Ernte-Bonus (PlantManager.OnScytheYieldBonus) —
///    kann bei derselben Ernte zusätzlich zum Dünger-Bonus auslösen, dann erscheinen beide
///    Texte nebeneinander statt übereinander (siehe spawnSpreadRadius).
///
/// Rein optisches Feedback — vorher gab's nur den Sound, und der geht im normalen
/// Spielgeschehen (Werkzeuggeräusche, Musik, Ambience) leicht unter, gerade bei der
/// Dünger-Chance, die ja nicht bei jeder Ernte auslöst.
///
/// Baut sich pro Fund selbst aus einem Welt-Text zusammen (kein Prefab nötig), steigt kurz
/// auf und blendet aus. Dreht sich jeden Frame zur Kamera — bei der isometrischen Ansicht
/// sähe man sonst nur die Kante. Startet mit einem kleinen zufälligen horizontalen Versatz
/// um die Tile herum: bei einer AoE-Ernte über mehrere gedüngte Felder können mehrere Texte
/// im selben Frame entstehen, ohne Versatz stünden die exakt übereinander.
///
/// Setup: auf ein beliebiges dauerhaftes GameObject in der SampleScene legen (z.B. neben
/// SoftlockHintTrigger).
/// </summary>
public class StarterSeedFoundFx : MonoBehaviour
{
    [Header("Bewegung")]
    [SerializeField] private float startYOffset = 0.4f;
    [SerializeField] private float riseHeight = 1.1f;
    [SerializeField] private float duration = 1.4f;

    [Tooltip("Zufälliger horizontaler Versatz um die Tile herum beim Spawn — verhindert, " +
             "dass mehrere Texte im selben Frame (z.B. AoE-Ernte über mehrere gedüngte " +
             "Felder) exakt übereinanderstehen.")]
    [SerializeField] private float spawnSpreadRadius = 0.35f;

    [Header("Not-Samen (Gras)")]
    [SerializeField] private float seedFontSize = 3.2f;
    [SerializeField] private Color seedTextColor = new Color(1f, 0.85f, 0.35f);

    [Header("Dünger-Bonus (Ernte)")]
    [SerializeField] private float fertilizerFontSize = 2.8f;
    [SerializeField] private Color fertilizerTextColor = new Color(0.55f, 0.9f, 0.4f);

    [Header("Sichel-Ernte-Bonus (jede Ernte)")]
    [SerializeField] private float scytheFontSize = 2.8f;
    [SerializeField] private Color scytheTextColor = new Color(1f, 0.55f, 0.3f);

    [Header("Optik (gemeinsam)")]
    [Tooltip("Fast schwarz statt dunkelbraun — auf dem braunen Erdboden ist ein " +
             "brauner Rand kaum vom Untergrund zu unterscheiden.")]
    [SerializeField] private Color outlineColor = new Color(0.04f, 0.02f, 0.01f);

    [Tooltip("Deutlich dicker als der TMP-Default, sonst geht der Text auf Erde/Gras " +
             "unter, besonders bei kleiner Schrift oder mehreren Texten gleichzeitig.")]
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.45f;

    private void OnEnable()
    {
        PlantManager.OnStarterSeedFound += HandleStarterSeedFound;
        PlantManager.OnFertilizedHarvestBonus += HandleFertilizedHarvestBonus;
        PlantManager.OnScytheYieldBonus += HandleScytheYieldBonus;
    }

    private void OnDisable()
    {
        PlantManager.OnStarterSeedFound -= HandleStarterSeedFound;
        PlantManager.OnFertilizedHarvestBonus -= HandleFertilizedHarvestBonus;
        PlantManager.OnScytheYieldBonus -= HandleScytheYieldBonus;
    }

    private void HandleStarterSeedFound(PlantType plant, Vector3 worldPos)
    {
        string label = plant != null ? $"+1 {plant.plantName}-Samen" : "+1 Samen";
        Spawn(worldPos, label, seedFontSize, seedTextColor);
    }

    private void HandleFertilizedHarvestBonus(PlantType plant, Vector3 worldPos)
    {
        string label = plant != null ? $"+1 {plant.plantName} (Dünger!)" : "+1 (Dünger!)";
        Spawn(worldPos, label, fertilizerFontSize, fertilizerTextColor);
    }

    private void HandleScytheYieldBonus(PlantType plant, Vector3 worldPos)
    {
        string label = plant != null ? $"+1 {plant.plantName} (Sichel!)" : "+1 (Sichel!)";
        Spawn(worldPos, label, scytheFontSize, scytheTextColor);
    }

    private void Spawn(Vector3 worldPos, string text, float fontSize, Color color)
    {
        Vector2 spread = Random.insideUnitCircle * spawnSpreadRadius;
        Vector3 spawnPos = worldPos + new Vector3(spread.x, startYOffset, spread.y);

        var go = new GameObject("BonusPopupFx");
        go.transform.position = spawnPos;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineColor = outlineColor;
        tmp.outlineWidth = outlineWidth;

        // outlineWidth/outlineColor allein reichen bei diesem Shader NICHT — das
        // Standard-Font-Material (TextMeshPro/Mobile/Distance Field) hat den Outline-Block
        // hinter einem shader_feature versteckt (OUTLINE_ON), das TMP nicht automatisch
        // mitsetzt, wenn man nur die Properties zuweist. Ohne das Keyword ignoriert der
        // Fragment-Shader _OutlineWidth/_OutlineColor komplett — der Text bleibt ohne Rand,
        // egal wie die beiden Felder oben stehen. `.fontMaterial` (statt fontSharedMaterial)
        // erzeugt dabei automatisch eine EIGENE Material-Instanz für dieses eine Popup, das
        // Standard-Material (und alle anderen Texte, die es benutzen) bleibt unangetastet.
        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");

        StartCoroutine(Animate(go, tmp));
    }

    private IEnumerator Animate(GameObject go, TextMeshPro tmp)
    {
        Vector3 start = go.transform.position;
        Vector3 end = start + Vector3.up * riseHeight;
        Color baseColor = tmp.color;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);

            go.transform.position = Vector3.Lerp(start, end, p);

            if (Camera.main != null)
                go.transform.rotation = Camera.main.transform.rotation;

            // Erst die zweite Hälfte ausblenden — sonst verblasst der Text, bevor man
            // ihn überhaupt gelesen hat.
            float alpha = p < 0.5f ? 1f : 1f - (p - 0.5f) / 0.5f;
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            yield return null;
        }

        Destroy(go);
    }
}
