using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CozyCrops.EditorTools
{
    /// <summary>
    /// Setzt das globale Lighting-Preset ("Warmer Nachmittag") auf alle Spiel-Szenen.
    ///
    /// Warum als Script und nicht per Hand im Inspector:
    /// - Reproduzierbar: alle drei Szenen bekommen garantiert exakt dieselben Werte
    /// - Nachvollziehbar: die Werte stehen hier als Konstanten, nicht versteckt in 5,9 MB Szenen-YAML
    /// - Team-tauglich: jeder kann es nachfahren, statt Screenshots von Inspector-Werten zu tauschen
    ///
    /// Rückgängig machen: über Git (die Szenen-Dateien werden geschrieben).
    /// "Nur aktuelle Szene" unterstützt zusätzlich Ctrl+Z.
    /// </summary>
    public static class CozyLightingSetup
    {
        // ─────────────────────────────────────────────────────────────
        // PRESET — hier schrauben, nicht im Code weiter unten
        // ─────────────────────────────────────────────────────────────

        // Ambient (Gradient): kühl von oben, warm von unten.
        // Der warme Ground-Bounce ist der Trick, der Lowpoly-Modelle
        // plastisch macht, ohne dass man Texturen braucht.
        const string AmbientSkyHex = "#8CB3DB";
        const string AmbientEquatorHex = "#C9BFA6";
        const string AmbientGroundHex = "#6E5B44";

        // Sonne
        const string SunHex = "#FFE7BF";
        const float SunIntensity = 1.55f;
        const float SunShadowStrength = 0.95f;
        const float SunPitch = 48f;
        const float SunYaw = -35f;

        // Fill Light: fakt Himmels-Bounce von der Gegenseite.
        // Ohne das saufen die schattenabgewandten Flächen komplett ab.
        // Bewusst schwach: zu viel Fill frisst genau die Schatten wieder weg,
        // die die Sonne gerade erzeugt hat.
        const string FillLightName = "Fill Light (Himmelsbounce)";
        const string FillHex = "#A9CBFF";
        const float FillIntensity = 0.22f;
        const float FillPitch = 18f;
        const float FillYaw = 145f;

        // Schatten-Qualität.
        //
        // Korrektur 2026-08-12: ShadowDistance stand auf 30, berechnet für orthographicSize 5.
        // Der CameraController zoomt aber bis size 20 — dort sieht man entlang der Blickachse
        // ~55 Units Boden, die Schatten brachen also mitten im Bild ab.
        //
        // Der Wert hier ist nur noch die Grundlinie (greift z.B. in der Marketplace-Szene).
        // Im Spiel überschreibt CameraController.ApplyShadowDistance() ihn zoomabhängig.
        // Deshalb wieder 4 Kaskaden: die Splits sind Bruchteile der Distanz, die nächste
        // Kaskade bleibt dadurch bei jedem Zoom-Level eng. Shadowmap auf 4096 — bei diesem
        // Low-Poly-Umfang kostet das praktisch nichts und gibt die Dichte zurück.
        const float ShadowDistance = 45f;
        const int ShadowCascades = 4;
        const int ShadowmapResolution = 4096;
        const float ShadowDepthBias = 0.05f;
        const float ShadowNormalBias = 0.15f;

        // Fog — gibt Tiefe, staffelt die Ebenen
        const string FogHex = "#E3DCC8";
        const float FogStart = 30f;
        const float FogEnd = 140f;

        const string ShadowTintHex = "#6B7FA3";

        // Post-Processing-Profil (ist im RP-Asset als Default-Volume-Profile hinterlegt,
        // gilt dadurch für alle Szenen)
        const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        static bool s_UseUndo;

        // ─────────────────────────────────────────────────────────────
        // MENÜ
        // ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/Cozy Crops/Lighting/Auf alle Szenen anwenden", priority = 0)]
        public static void ApplyToAllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var paths = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled && !string.IsNullOrEmpty(s.path))
                    paths.Add(s.path);
            }

            if (paths.Count == 0)
            {
                Debug.LogError("[CozyLighting] Keine Szenen in den Build Settings. Abbruch.");
                return;
            }

            var returnTo = SceneManager.GetActiveScene().path;

            s_UseUndo = false;
            ApplyPipelineAsset();
            ApplyVolumeProfile();

            foreach (var path in paths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ApplySceneLighting();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[CozyLighting] {scene.name} aktualisiert.");
            }

            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(returnTo))
                EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);

            Debug.Log($"[CozyLighting] Fertig — {paths.Count} Szenen. Rückgängig: git checkout Assets/Scenes Assets/Settings");
        }

        [MenuItem("Tools/Cozy Crops/Lighting/Nur aktuelle Szene", priority = 1)]
        public static void ApplyToCurrentScene()
        {
            s_UseUndo = true;
            Undo.SetCurrentGroupName("Cozy Lighting anwenden");
            int group = Undo.GetCurrentGroup();

            ApplyPipelineAsset();
            ApplyVolumeProfile();
            ApplySceneLighting();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[CozyLighting] {SceneManager.GetActiveScene().name} aktualisiert (Ctrl+Z geht).");
        }

        /// <summary>
        /// Schaltet Cast Shadows auf allen Deko-/Prop-Renderern der aktuellen Szene wieder an.
        ///
        /// Hintergrund: In SampleScene hatte jemand einmal alles markiert und Cast Shadows
        /// abgeschaltet — 1450 gebackene Tiles plus 96 Prefab-Instanzen mit Scene-Override.
        /// Deshalb warfen Büsche, Bäume & Co. gar keine Schatten.
        ///
        /// Die flachen Boden-Tiles bleiben bewusst aus: es sind Cubes auf Scale (1, 0.1, 1),
        /// alle auf exakt derselben Höhe. Ihr Schatten würde auf das gleich hohe Nachbar-Tile
        /// fallen — das gibt Shadow Acne und dreckige Nähte, keinen schönen Schatten.
        /// </summary>
        [MenuItem("Tools/Cozy Crops/Lighting/Schatten reparieren (aktuelle Szene)", priority = 2)]
        public static void RepairShadowCasters()
        {
            int fixedCount = 0, tilesSkipped = 0, utilitySkipped = 0, alreadyOn = 0;

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var r in renderers)
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                if (IsFlatGroundTile(r)) { tilesSkipped++; continue; }
                if (IsUtilityVisual(r)) { utilitySkipped++; continue; }
                if (r.shadowCastingMode == ShadowCastingMode.On) { alreadyOn++; continue; }

                Undo.RecordObject(r, "Cast Shadows aktivieren");
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;

                // Ohne das bleibt die Änderung an einer Prefab-Instanz nicht erhalten —
                // der bestehende m_CastShadows-Override würde sie beim Reload überschreiben.
                if (PrefabUtility.IsPartOfPrefabInstance(r))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);

                EditorUtility.SetDirty(r);
                fixedCount++;
            }

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[CozyLighting] {scene.name}: {fixedCount} Renderer werfen jetzt Schatten " +
                      $"(bereits an: {alreadyOn}, Boden-Tiles übersprungen: {tilesSkipped}, " +
                      $"UI/Overlay übersprungen: {utilitySkipped}). Szene noch speichern!");
        }

        static bool IsFlatGroundTile(Renderer r)
        {
            if (r.GetComponent<TileMarker>() != null) return true;
            if (r.GetComponentInParent<FarmTileVisual>() != null) return true;
            return r.name.Contains("Tile Prefab"); // Border-Tiles haben keinen TileMarker
        }

        static readonly string[] UtilityNameHints =
        {
            "Marker", "Highlight", "Overlay", "Preview", "Selection", "Select", "unsichtbar"
        };

        static bool IsUtilityVisual(Renderer r)
        {
            foreach (var hint in UtilityNameHints)
            {
                if (r.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            // AoE-Preview- und Auswahl-Quads liegen flach auf dem Boden.
            // Die dürfen auf keinen Fall Schatten werfen.
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                foreach (var hint in UtilityNameHints)
                {
                    if (mat.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        [MenuItem("Tools/Cozy Crops/Lighting/Nur Post-Processing + Pipeline", priority = 3)]
        public static void ApplyPostOnly()
        {
            s_UseUndo = false;
            ApplyPipelineAsset();
            ApplyVolumeProfile();
            AssetDatabase.SaveAssets();
            Debug.Log("[CozyLighting] Pipeline-Asset und Volume-Profil aktualisiert.");
        }

        // ─────────────────────────────────────────────────────────────
        // SZENE
        // ─────────────────────────────────────────────────────────────

        static void ApplySceneLighting()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            ApplyEnvironment(sceneName);
            var sun = ApplySun();
            ApplyFillLight();
            int cams = ApplyCameras();

            RenderSettings.sun = sun;

            if (cams == 0)
                Debug.LogWarning($"[CozyLighting] {sceneName}: keine Base-Kamera gefunden — Post-Processing nicht gesetzt.");
        }

        static void ApplyEnvironment(string sceneName)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight; // = "Gradient" im Lighting-Fenster
            RenderSettings.ambientSkyColor = Hex(AmbientSkyHex);
            RenderSettings.ambientEquatorColor = Hex(AmbientEquatorHex);
            RenderSettings.ambientGroundColor = Hex(AmbientGroundHex);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.subtractiveShadowColor = Hex(ShadowTintHex);

            // Im Hauptmenü stört Fog nur — da steht die Kamera fix und nah dran.
            bool wantsFog = !sceneName.Contains("MainMenu");
            RenderSettings.fog = wantsFog;
            if (wantsFog)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = Hex(FogHex);
                RenderSettings.fogStartDistance = FogStart;
                RenderSettings.fogEndDistance = FogEnd;
            }
        }

        static Light ApplySun()
        {
            Light sun = null;
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Stärkstes Directional Light = die Sonne. Das Fill Light ist explizit ausgenommen,
            // sonst befördert sich das Script bei jedem zweiten Lauf selbst zur Sonne.
            foreach (var l in lights)
            {
                if (l.type != LightType.Directional) continue;
                if (l.gameObject.name == FillLightName) continue;
                if (sun == null || l.intensity > sun.intensity) sun = l;
            }

            if (sun == null)
            {
                var go = new GameObject("Directional Light");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                if (s_UseUndo) Undo.RegisterCreatedObjectUndo(go, "Sonne erstellen");
            }
            else if (s_UseUndo)
            {
                Undo.RecordObject(sun, "Sonne konfigurieren");
                Undo.RecordObject(sun.transform, "Sonne ausrichten");
            }

            sun.color = Hex(SunHex);
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = SunShadowStrength;
            sun.transform.rotation = Quaternion.Euler(SunPitch, SunYaw, 0f);

            EditorUtility.SetDirty(sun);
            return sun;
        }

        static void ApplyFillLight()
        {
            Light fill = null;
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.gameObject.name == FillLightName) { fill = l; break; }
            }

            if (fill == null)
            {
                var go = new GameObject(FillLightName);
                fill = go.AddComponent<Light>();
                fill.type = LightType.Directional;
                if (s_UseUndo) Undo.RegisterCreatedObjectUndo(go, "Fill Light erstellen");
            }
            else if (s_UseUndo)
            {
                Undo.RecordObject(fill, "Fill Light konfigurieren");
                Undo.RecordObject(fill.transform, "Fill Light ausrichten");
            }

            fill.type = LightType.Directional;
            fill.color = Hex(FillHex);
            fill.intensity = FillIntensity;
            fill.shadows = LightShadows.None; // zweite Schattenquelle würde den Cartoon-Look zermatschen
            fill.transform.rotation = Quaternion.Euler(FillPitch, FillYaw, 0f);

            EditorUtility.SetDirty(fill);
        }

        static int ApplyCameras()
        {
            int count = 0;
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var cam in cams)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                if (data.renderType != CameraRenderType.Base) continue; // Overlay-Kameras können kein Post

                if (s_UseUndo) Undo.RecordObject(data, "Kamera Post-Processing");

                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;

                EditorUtility.SetDirty(data);
                count++;
            }

            return count;
        }

        // ─────────────────────────────────────────────────────────────
        // PIPELINE + POST
        // ─────────────────────────────────────────────────────────────

        static void ApplyPipelineAsset()
        {
            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null)
            {
                Debug.LogWarning("[CozyLighting] Kein URP-Asset aktiv — Pipeline-Settings übersprungen.");
                return;
            }

            // SerializedObject statt Properties: überlebt URP-Versionswechsel,
            // bei denen die Setter mal public und mal internal sind.
            var so = new SerializedObject(rp);

            var grading = so.FindProperty("m_ColorGradingMode");
            if (grading != null) grading.intValue = 1; // 0 = LDR, 1 = HDR

            var msaa = so.FindProperty("m_MSAA");
            if (msaa != null) msaa.intValue = 4; // harte Lowpoly-Kanten brauchen das am dringendsten

            var dist = so.FindProperty("m_ShadowDistance");
            if (dist != null) dist.floatValue = ShadowDistance;

            var cascades = so.FindProperty("m_ShadowCascadeCount");
            if (cascades != null) cascades.intValue = ShadowCascades;

            var shadowRes = so.FindProperty("m_MainLightShadowmapResolution");
            if (shadowRes != null) shadowRes.intValue = ShadowmapResolution;

            // Hoher Normal Bias schiebt Schatten von ihrem Caster weg und lässt
            // kleine Objekte (Pflanzen!) fast schattenlos aussehen. 0.5 war zu viel.
            var depthBias = so.FindProperty("m_ShadowDepthBias");
            if (depthBias != null) depthBias.floatValue = ShadowDepthBias;

            var normalBias = so.FindProperty("m_ShadowNormalBias");
            if (normalBias != null) normalBias.floatValue = ShadowNormalBias;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);

            Debug.Log($"[CozyLighting] Pipeline '{rp.name}': HDR-Grading, MSAA 4x, " +
                      $"Shadow Distance {ShadowDistance} ({ShadowCascades} Kaskaden, " +
                      $"{ShadowmapResolution}px), Bias {ShadowDepthBias}/{ShadowNormalBias}");

            ApplySsao();
        }

        /// <summary>
        /// SSAO läuft schon als Renderer Feature, war aber sehr zahm eingestellt.
        /// Bei 1 Unit = 1 Tile ist Radius 0.3 zu klein, um Kontaktschatten dort zu
        /// erzeugen wo eine Pflanze den Boden berührt — und genau das lässt Objekte
        /// "aufsitzen" statt zu schweben.
        /// </summary>
        static void ApplySsao()
        {
            const string rendererPath = "Assets/Settings/PC_Renderer.asset";

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(rendererPath);
            if (subAssets == null || subAssets.Length == 0)
            {
                Debug.LogWarning($"[CozyLighting] Renderer nicht gefunden: {rendererPath}");
                return;
            }

            foreach (var asset in subAssets)
            {
                if (asset == null) continue;
                if (asset.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;

                var so = new SerializedObject(asset);
                SetFloat(so, "m_Settings.Intensity", 0.75f);
                SetFloat(so, "m_Settings.Radius", 0.5f);
                SetFloat(so, "m_Settings.DirectLightingStrength", 0.35f);
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(asset);
                Debug.Log("[CozyLighting] SSAO verstärkt (Intensity 0.75, Radius 0.5).");
                return;
            }

            Debug.LogWarning("[CozyLighting] Kein SSAO Renderer Feature auf PC_Renderer gefunden.");
        }

        static void SetFloat(SerializedObject so, string path, float value)
        {
            var prop = so.FindProperty(path);
            if (prop != null) prop.floatValue = value;
            else Debug.LogWarning($"[CozyLighting] Property nicht gefunden: {path}");
        }

        static void ApplyVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"[CozyLighting] Volume-Profil nicht gefunden: {ProfilePath}");
                return;
            }

            var tone = GetOrAdd<Tonemapping>(profile);
            tone.active = true;
            tone.mode.Override(TonemappingMode.Neutral);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(true);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.35f);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.contrast.Override(8f);
            color.saturation.Override(12f); // Lowpoly verträgt kräftige Farben
            color.colorFilter.Override(Hex("#FFF6E8"));

            var wb = GetOrAdd<WhiteBalance>(profile);
            wb.active = true;
            wb.temperature.Override(8f);
            wb.tint.Override(2f);

            // Das hier macht den eigentlichen "Cartoon"-Eindruck:
            // kühle Schatten gegen warme Lichter.
            var split = GetOrAdd<SplitToning>(profile);
            split.active = true;
            split.shadows.Override(Hex("#46617F"));
            split.highlights.Override(Hex("#FFD9A3"));
            split.balance.Override(-12f);

            EditorUtility.SetDirty(profile);
            Debug.Log($"[CozyLighting] Volume-Profil '{profile.name}' aktualisiert.");
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet<T>(out var component) ? component : profile.Add<T>(true);
        }

        static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c))
                return c;

            Debug.LogWarning($"[CozyLighting] Ungültiger Farbwert: {hex}");
            return Color.magenta;
        }
    }
}
