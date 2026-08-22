// Screen-Space Outline für Quest-Highlighting.
//
// Läuft als URP "Full Screen Pass Renderer Feature" auf der Hauptkamera (siehe
// Setup-Anleitung). _BlitTexture ist das fertige Kamerabild (von URP autom.
// befüllt), _HighlightMask ist die Silhouette aus der separaten Mask-Kamera
// (weiß = highlighted Objekt, schwarz = Rest). Für jeden Pixel wird geschaut
// ob ein Nachbar-Texel "heller" ist als das Zentrum (= Rand der Silhouette)
// und an der Stelle die Outline-Farbe über das Bild gelegt — Dicke ist damit
// eine konstante Anzahl Pixel, unabhängig von 3D-Blickwinkel/Mesh-Normalen.
//
// Falls Unity beim Import "Vert"/"Varyings" nicht findet (URP-Version hat das
// Blit.hlsl-API geändert): stattdessen über Assets > Create > Shader >
// Fullscreen Shader ein neues Shader-Template anlegen (das bringt die für
// eure URP-Version exakt passenden Structs mit) und nur den frag()-Body unten
// reinkopieren.
Shader "CozyCrops/HighlightOutlineComposite"
{
    Properties
    {
        _HighlightMask ("Highlight Mask", 2D) = "black" {}
        _OutlineColor  ("Farbe", Color) = (1, 0.85, 0.2, 1)
        // Default bewusst auf 1 Texel — bei Bilinear-Sampling der Maske reicht das
        // für eine saubere duenne Kontur, hoehere Werte wirken schnell nach Glow.
        _Thickness     ("Dicke (Texel)", Range(1, 8)) = 1

        [Header(Puls)]
        _PulseSpeed    ("Puls Tempo (Hz)", Range(0.0, 4.0)) = 1.1
        // Puls laeuft auf der Deckkraft, NICHT auf der Dicke. Eine schwankende Dicke
        // muesste staendig andere Texel als Rand einstufen — das flimmert sichtbar,
        // genau der Fehler den die Hüllen-Variante frueher hatte.
        _PulseAmount   ("Puls Staerke", Range(0.0, 1.0)) = 0.45

        [Header(Verdeckung)]
        // Toleranz in WELT-METERN. Zu klein -> das Objekt verdeckt sich selbst und die
        // Kontur flackert; zu gross -> die Kontur scheint durch duenne Waende.
        _DepthBias     ("Tiefen Toleranz (Meter)", Range(0.0, 1.0)) = 0.05

        [Header(Debug)]
        // 0 = aus, 1 = Szenentiefe, 2 = Maskentiefe, 3 = "gilt als sichtbar"
        // Zur Fehlersuche bei der Verdeckung. Siehe Kommentar im Fragment-Shader.
        _DebugView     ("Debug Ansicht", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "HighlightOutlineComposite"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            // Core.hlsl zuerst: Blit.hlsl benutzt Makros wie TEXTURE2D_X, die es nicht
            // selbst mitbringt (sonst "unrecognized identifier 'TEXTURE2D_X'").
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_HighlightMask);
            SAMPLER(sampler_HighlightMask);
            float4 _HighlightMask_TexelSize;

            half4 _OutlineColor;
            float _Thickness;
            float _PulseSpeed;
            float _PulseAmount;
            float _DepthBias;
            float _DebugView;

            /// Szenentiefe an dieser Stelle, als Abstand in Metern — damit sie mit der
            /// Maske vergleichbar ist, die ebenfalls Meter speichert.
            float SceneEyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);

                if (unity_OrthoParams.w > 0.5)
                {
                    // Orthografisch: die Tiefe liegt linear zwischen Near und Far,
                    // LinearEyeDepth() gilt dafür nicht (das rechnet perspektivisch).
                #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }

                // Perspektivisch: _ZBufferParams trägt die Invertierung bereits in sich.
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            /// Liegt an dieser Stelle ein SICHTBARER Teil eines Highlight-Objekts?
            /// Prüft zwei Dinge: steht in der Maske überhaupt etwas, und wird es von
            /// der Szene verdeckt.
            float MaskInside(float2 uv)
            {
                float maskEye = SAMPLE_TEXTURE2D(_HighlightMask, sampler_HighlightMask, uv).r;

                // 0 = nichts gezeichnet. Echte Geometrie liegt nie näher als die Near-Plane.
                if (maskEye <= 0.0001) return 0.0;

                // Beides in Metern, also gilt schlicht: weiter weg als die Szene = verdeckt.
                return maskEye <= SceneEyeDepth(uv) + _DepthBias ? 1.0 : 0.0;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 uv    = input.texcoord;
                half3  scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;

                // --- Fehlersuche Verdeckung ---
                // _DebugView am Material umstellen, dann zeigt der Pass Rohdaten statt Bild:
                //   1 = Szenentiefe in Metern als Graustufe (dunkel = nah, 50 m = weiß)
                //   2 = Maskentiefe, gleicher Maßstab in Grün. Schwarz = kein Objekt.
                //   3 = Urteil: grün = gilt als sichtbar, rot = gilt als verdeckt.
                // Bei 1 und 2 müssen die Helligkeiten am selben Objekt ÜBEREINSTIMMEN —
                // tun sie das nicht, messen die beiden Kameras Unterschiedliches.
                if (_DebugView > 0.5)
                {
                    // Beide Tiefen sind jetzt Meter, also mit demselben Massstab lesbar:
                    // je heller, desto weiter weg. 50 m = weiss.
                    if (_DebugView < 1.5)
                        return half4(saturate(SceneEyeDepth(uv) / 50.0).xxx, 1);

                    if (_DebugView < 2.5)
                    {
                        float m = SAMPLE_TEXTURE2D(_HighlightMask, sampler_HighlightMask, uv).r;
                        if (m <= 0.0001) return half4(0, 0, 0, 1);          // schwarz = leer
                        return half4(0, saturate(m / 50.0), 0, 1);          // gruen = Abstand
                    }
                    // Ansicht 3: Farbcodiert, damit man Maske und Urteil zugleich sieht.
                    //   schwarz = hier ist gar kein Highlight-Objekt
                    //   grün    = Objekt da UND gilt als sichtbar
                    //   rot     = Objekt da, aber als verdeckt eingestuft
                    {
                        float m = SAMPLE_TEXTURE2D(_HighlightMask, sampler_HighlightMask, uv).r;
                        if (m <= 0.0001) return half4(0, 0, 0, 1);
                        return MaskInside(uv) > 0.5 ? half4(0, 1, 0, 1) : half4(1, 0, 0, 1);
                    }
                }

                float2 texel = _HighlightMask_TexelSize.xy * _Thickness;
                float  center = MaskInside(uv);

                // 4 Nachbarn reichen für eine saubere, gleichmäßige Kontur und sind
                // billiger als ein voller 8-Tap-Sobel — für einen dünnen Highlight-Rand
                // fällt der Unterschied kaum auf.
                float neighborMax = center;
                neighborMax = max(neighborMax, MaskInside(uv + float2( texel.x, 0)));
                neighborMax = max(neighborMax, MaskInside(uv + float2(-texel.x, 0)));
                neighborMax = max(neighborMax, MaskInside(uv + float2(0,  texel.y)));
                neighborMax = max(neighborMax, MaskInside(uv + float2(0, -texel.y)));

                // Rand = ein Nachbar ist "drin" (Maske), das Zentrum selbst aber (noch)
                // nicht komplett drin -> das ist genau der äußere Kontur-Pixel.
                float edge = saturate(neighborMax - center);

                // 0..1-Welle, nie ganz auf null: die Kontur soll atmen, nicht blinken.
                float wave  = (sin(_Time.y * _PulseSpeed * 6.2831853) + 1.0) * 0.5;
                float pulse = lerp(1.0 - _PulseAmount, 1.0, wave);

                half3 result = lerp(scene, _OutlineColor.rgb, edge * _OutlineColor.a * pulse);
                return half4(result, 1);
            }
            ENDHLSL
        }
    }
}
