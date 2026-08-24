// Reine Masken-Shader für das Quest-Highlighting: zeichnet das Objekt
// deckend weiß, unbeleuchtet. Läuft NICHT in der normalen Kamera (Layer
// "Highlight" ist aus deren Culling Mask ausgeschlossen), sondern nur in der
// separaten Mask-Kamera (siehe HighlightMaskCameraSync.cs / Setup-Anleitung).
// Das Ergebnis ist eine Silhouette in der HighlightMaskRT, die
// HighlightOutlineComposite.shader dann als Kontur um die Silhouette zeichnet.
Shader "CozyCrops/HighlightMask"
{
    Properties
    {
        [HideInInspector] _HighlightMode ("Highlight Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "HighlightMask"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _HighlightMode;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float  viewDepth   : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                OUT.positionHCS = TransformWorldToHClip(positionWS);

                // Abstand vor der Kamera in Weltmetern. In Unitys View-Space blickt die
                // Kamera entlang -Z, deshalb das Vorzeichen.
                OUT.viewDepth   = -TransformWorldToView(positionWS).z;
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                // Der ABSTAND zur Kamera in Metern — nicht der rohe Depth-Buffer-Wert.
                //
                // Der Rohwert wäre naheliegender, ist aber eine Falle: ob 0 die nahe oder
                // die ferne Ebene bedeutet, hängt von Plattform und Rendermodus ab. Beim
                // Vergleich zweier Kameras muss man sich dann sicher sein, dass BEIDE
                // dieselbe Konvention benutzen — und genau das stimmte hier nicht (Maske
                // nicht invertiert, _CameraDepthTexture invertiert), wodurch immer
                // "sichtbar" herauskam.
                //
                // Meter haben keine Konvention: größer ist immer weiter weg. 0 bleibt frei
                // als Markierung für "hier ist gar nichts", weil echte Geometrie nie näher
                // als die Near-Plane liegt.
                // Vorzeichen kodiert die Darstellungsart im bestehenden RFloat-Ziel:
                // positiv = Quest, negativ = Maus-Hover. Der Betrag bleibt die Tiefe.
                float encodedDepth = lerp(IN.viewDepth, -IN.viewDepth, saturate(_HighlightMode));
                return float4(encodedDepth, 0, 0, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
