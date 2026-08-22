using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Quest-Highlighting für NPCs/Interactables: baut pro MeshRenderer/
/// SkinnedMeshRenderer im Ziel eine unsichtbare Silhouetten-Kopie auf dem
/// Layer "Highlight" (CozyCrops/HighlightMask-Shader). Diese Kopie wird nur
/// von einer separaten Mask-Kamera gesehen (siehe Setup-Anleitung) — die
/// Hauptkamera rendert den Layer gar nicht. Der eigentliche Kontur-Rand
/// entsteht dann als Screen-Space-Effekt (CozyCrops/HighlightOutlineComposite)
/// auf der Hauptkamera, nicht hier.
///
/// Wichtig: Farbe/Dicke der Outline sind kein Feld auf dieser Component,
/// sondern auf dem gemeinsamen Composite-Material der Renderer Feature — der
/// Effekt ist global für den ganzen "Highlight"-Layer, nicht pro Objekt
/// einzeln einfärbbar. Für dieses Projekt reicht das (immer max. ein
/// Quest-Ziel gleichzeitig hervorgehoben).
///
/// Einfach auf den NPC/das Ziel-Prefab legen und per SetHighlighted(true) von
/// außen ansteuern (z.B. vom MissionManager wenn das aktuelle Objective
/// dieses Ziel betrifft).
/// </summary>
[DisallowMultipleComponent]
public class HighlightOutline : MonoBehaviour, IHighlightVisual
{
    /// <summary>
    /// Layer, auf dem die Masken-Kopien liegen. Nur die Mask-Kamera darf ihn rendern —
    /// sieht ihn eine normale Kamera, malt sie die weiße Silhouette direkt über das
    /// Objekt und der NPC wirkt grau. <see cref="HighlightMaskCameraSync"/> nimmt den
    /// Layer deshalb allen anderen Kameras automatisch weg.
    /// </summary>
    public const string HighlightLayerName = "Highlight";

    [SerializeField] private bool startHighlighted;

    private readonly List<Renderer> clones = new();
    private static Material sharedMaskMaterial;
    private static int highlightLayer = -1;
    private bool built;

    public bool IsHighlighted { get; private set; }

    void Awake()
    {
        BuildClones();
        SetHighlighted(startHighlighted);
    }

    public void SetHighlighted(bool on)
    {
        if (!built) BuildClones();
        IsHighlighted = on;

        for (int i = clones.Count - 1; i >= 0; i--)
        {
            // Laufzeit-Objekte können ihre Renderer unter uns wegräumen — bei Pflanzen
            // zerstört PlantManager.UpdateVisual() das komplette Visual pro Wachstums-
            // stufe. Tote Einträge hier still aussortieren statt auf eine
            // NullReferenceException zu warten.
            if (clones[i] == null) { clones.RemoveAt(i); continue; }
            clones[i].enabled = on;
        }
    }

    public void Toggle() => SetHighlighted(!IsHighlighted);

    /// <summary>
    /// Baut die Masken-Kopien neu auf. Nötig, wenn sich die Renderer nach dem Awake
    /// ändern — also überall wo zur Laufzeit Geometrie getauscht wird (Wachstumsstufen,
    /// nachgeladene Modelle, Ausrüstungswechsel).
    /// </summary>
    public void Rebuild()
    {
        for (int i = 0; i < clones.Count; i++)
            if (clones[i] != null) Destroy(clones[i].gameObject);

        clones.Clear();
        built = false;

        BuildClones();
        SetHighlighted(IsHighlighted);
    }

    private void BuildClones()
    {
        if (built) return;
        built = true;

        if (highlightLayer < 0)
        {
            highlightLayer = LayerMask.NameToLayer(HighlightLayerName);
            if (highlightLayer < 0)
            {
                Debug.LogWarning($"{nameof(HighlightOutline)}: Layer '{HighlightLayerName}' existiert nicht " +
                                  "(Project Settings > Tags and Layers anlegen). Highlighting bleibt aus.", this);
                return;
            }
        }

        if (sharedMaskMaterial == null)
        {
            var shader = Shader.Find("CozyCrops/HighlightMask");
            if (shader == null)
            {
                Debug.LogWarning($"{nameof(HighlightOutline)}: Shader 'CozyCrops/HighlightMask' nicht gefunden.", this);
                return;
            }
            sharedMaskMaterial = new Material(shader);
        }

        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
        {
            var mesh = mr.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null) clones.Add(CreateClone(mr, mesh, null));
        }

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            clones.Add(CreateClone(smr, smr.sharedMesh, smr));
    }

    private Renderer CreateClone(Renderer source, Mesh mesh, SkinnedMeshRenderer skinnedSource)
    {
        var go = new GameObject($"{source.name}_HighlightMask");
        go.transform.SetParent(source.transform, false);
        go.layer = highlightLayer;

        Renderer clone;
        if (skinnedSource != null)
        {
            var skinned = go.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.bones = skinnedSource.bones;
            skinned.rootBone = skinnedSource.rootBone;
            clone = skinned;
        }
        else
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            clone = go.AddComponent<MeshRenderer>();
        }

        var mats = new Material[Mathf.Max(1, source.sharedMaterials.Length)];
        for (int i = 0; i < mats.Length; i++) mats[i] = sharedMaskMaterial;
        clone.sharedMaterials = mats;
        clone.shadowCastingMode = ShadowCastingMode.Off;
        clone.receiveShadows = false;
        clone.enabled = false;

        return clone;
    }
}
