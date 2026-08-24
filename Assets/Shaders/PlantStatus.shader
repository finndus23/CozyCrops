// Status-Anzeige über einer Pflanze: Wachstumsbogen + Bedürfnis-Symbol.
//
// Bewusst eine ANDERE Bildsprache als CozyCrops/TileIndicator:
// der Tile-Indikator ist flach am Boden, eckig und hart (Eckklammern, Puls) —
// das liest sich als UI-Element auf dem Raster. Hier geht es dagegen um den
// Zustand eines lebenden Dings, also: rund, schwebend, weiche Kanten, kein
// Rahmen und kein Puls. Wer beides gleichzeitig sieht, soll nicht zweimal
// dasselbe Element wahrnehmen.
//
// Alles ist prozedural aus den UVs gezeichnet — kein Sprite, keine Atlas-Pflege.
Shader "CozyCrops/PlantStatus"
{
    Properties
    {
        [MainColor] _BaseColor    ("Farbe", Color) = (0.45, 0.85, 0.35, 1)

        [Header(Wachstumsbogen)]
        _Progress     ("Fortschritt", Range(0.0, 1.0)) = 0.0
        _RingRadius   ("Radius", Range(0.05, 0.5)) = 0.34
        _RingWidth    ("Breite", Range(0.01, 0.2)) = 0.055
        _TrackAlpha   ("Alpha der ungefuellten Bahn", Range(0.0, 1.0)) = 0.18

        [Header(Symbol)]
        // 0 = keins, 1 = Wassertropfen, 2 = Funkeln (erntereif),
        // 3 = Saatgut, 4 = Verkauf, 5 = Werkzeug, 6 = Lizenz.
        _Symbol       ("Symbol (0/1/2)", Float) = 0
        _SymbolScale  ("Symbolgroesse", Range(0.1, 1.0)) = 0.42

        [Header(Kanten)]
        _EdgeSoftness ("Kantenweichheit", Range(0.001, 0.15)) = 0.02
        _Scale        ("Groesse in Welteinheiten", Float) = 0.5

        // Nur wenn ein Symbol anliegt. Eine bloss wachsende Pflanze soll ruhig
        // dastehen — wippt alles gleichzeitig, wird ein volles Feld unruhig.
        _BobAmount    ("Wippen (nur mit Symbol)", Range(0.0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            // Transparent+5: über den Pflanzen, aber UNTER dem Tile-Indikator (+10).
            // Liegt der Cursor auf dem Feld, gewinnt die Aktion die Aufmerksamkeit.
            "Queue"           = "Transparent+5"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PlantStatusForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // LEqual: verschwindet hinter Scheune und Bäumen statt durchzuscheinen.
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Progress;
                float  _RingRadius;
                float  _RingWidth;
                float  _TrackAlpha;
                float  _Symbol;
                float  _SymbolScale;
                float  _EdgeSoftness;
                float  _Scale;
                float  _BobAmount;
            CBUFFER_END

            // Billboard im Vertex-Shader statt über transform.LookAt im Update:
            // spart einen Transform-Schreibzugriff pro Pflanze und Frame, und das
            // Quad steht garantiert exakt zur Kamera — auch während die Kamera dreht.
            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));

                // Wippen nur mit Symbol: das ist der Zustand, der eine Handlung
                // verlangt, und Bewegung ist der einzige Reiz, den man auch am
                // Bildrand noch bemerkt.
                centerWS.y += (_Symbol > 0.5) ? sin(_Time.y * 3.2) * _BobAmount : 0.0;

                float3 right    = UNITY_MATRIX_V._m00_m01_m02;
                float3 up       = UNITY_MATRIX_V._m10_m11_m12;

                float3 posWS = centerWS
                             + right * (IN.positionOS.x * _Scale)
                             + up    * (IN.positionOS.y * _Scale);

                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv          = IN.uv;
                return OUT;
            }

            // Weiche Vereinigung zweier Formen. Damit wird aus zwei Kreisen ein
            // Tropfen, ohne dass man eine Dreiecks-Distanzfunktion braucht.
            float smin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            float sdCircle(float2 p, float2 c, float r) { return length(p - c) - r; }

            float sdBox(float2 p, float2 halfSize)
            {
                float2 d = abs(p) - halfSize;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            float sdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // Wassertropfen: dicker Bauch unten, spitz nach oben.
            float sdDrop(float2 p, float s)
            {
                float body = sdCircle(p, float2(0.0, -0.06) * s, 0.17 * s);
                float tip  = sdCircle(p, float2(0.0,  0.16) * s, 0.04 * s);
                return smin(body, tip, 0.15 * s);
            }

            // Funkeln: konkaver Vierzackstern (Astroide). Liest sich als "fertig,
            // hol es ab" — und ist bewusst NICHT rund, damit es sich vom Bogen absetzt.
            float sdSparkle(float2 p, float s)
            {
                float2 q = abs(p) / max(s, 1e-4);
                return pow(q.x, 0.5) + pow(q.y, 0.5) - 0.62;
            }

            // Saatgut-Händler: Blatt/Samen mit kurzer Mittelrippe.
            float sdSeed(float2 p)
            {
                float leaf = max(sdCircle(p, float2(-0.075, -0.035), 0.19),
                                 sdCircle(p, float2( 0.075,  0.035), 0.19));
                float stem = sdSegment(p, float2(-0.12, -0.15), float2(0.12, 0.15)) - 0.018;
                return min(leaf, stem);
            }

            // Verkaufs-Händler: kleine Münze mit stilisiertem Währungsstrich.
            float sdCoin(float2 p)
            {
                float rim = abs(length(p) - 0.17) - 0.025;
                float vertical = sdSegment(p, float2(0.0, -0.12), float2(0.0, 0.12)) - 0.018;
                float middle = sdSegment(p, float2(-0.07, 0.0), float2(0.07, 0.0)) - 0.018;
                return min(rim, min(vertical, middle));
            }

            // Werkzeug-Händler: schräger Hammer, auch in sehr kleiner Darstellung lesbar.
            float sdHammer(float2 p)
            {
                float handle = sdSegment(p, float2(-0.13, -0.18), float2(0.07, 0.08)) - 0.027;
                float2 q = float2((p.x + p.y) * 0.7071068,
                                  (p.y - p.x) * 0.7071068);
                float head = sdBox(q - float2(0.0, 0.13), float2(0.16, 0.055));
                return min(handle, head);
            }

            // Lizenzamt: Dokumentumriss mit zwei Textzeilen.
            float sdDocument(float2 p)
            {
                float page = abs(sdBox(p, float2(0.15, 0.20))) - 0.018;
                float lineA = sdSegment(p, float2(-0.085, 0.055), float2(0.085, 0.055)) - 0.015;
                float lineB = sdSegment(p, float2(-0.085, -0.035), float2(0.045, -0.035)) - 0.015;
                return min(page, min(lineA, lineB));
            }

            float fillMask(float dist, float softness)
            {
                return 1.0 - smoothstep(-softness, softness, dist);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.uv - 0.5;
                float  r = length(p);

                // ── Wachstumsbogen ────────────────────────────────────────────
                // Ring als Betrag des Abstands zum Sollradius. Der Bogen läuft im
                // Uhrzeigersinn ab oben — gleiche Leserichtung wie überall sonst.
                float band  = 1.0 - smoothstep(_RingWidth - _EdgeSoftness,
                                               _RingWidth + _EdgeSoftness,
                                               abs(r - _RingRadius));

                float angle = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                float grown = step(angle, _Progress);

                // Ungefüllte Bahn bleibt schwach sichtbar: ohne sie sieht ein Feld
                // frisch gesetzter Pflanzen aus, als würde gar nichts passieren.
                float ringAlpha = band * lerp(_TrackAlpha, 1.0, grown);

                // ── Symbol ────────────────────────────────────────────────────
                float symbolAlpha = 0.0;

                if (_Symbol > 0.5 && _Symbol < 1.5)
                    symbolAlpha = fillMask(sdDrop(p, _SymbolScale), _EdgeSoftness);
                else if (_Symbol > 1.5 && _Symbol < 2.5)
                    symbolAlpha = fillMask(sdSparkle(p, _SymbolScale), _EdgeSoftness * 3.0);
                else if (_Symbol > 2.5 && _Symbol < 3.5)
                    symbolAlpha = fillMask(sdSeed(p), _EdgeSoftness);
                else if (_Symbol > 3.5 && _Symbol < 4.5)
                    symbolAlpha = fillMask(sdCoin(p), _EdgeSoftness);
                else if (_Symbol > 4.5 && _Symbol < 5.5)
                    symbolAlpha = fillMask(sdHammer(p), _EdgeSoftness);
                else if (_Symbol > 5.5)
                    symbolAlpha = fillMask(sdDocument(p), _EdgeSoftness);

                float alpha = saturate(max(ringAlpha, symbolAlpha)) * _BaseColor.a;

                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
