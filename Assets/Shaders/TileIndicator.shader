// Tile-Indikator für Hover- und AoE-Preview.
//
// Ersetzt den alten Ansatz (transparenter Cube mit URP/Lit): der war nur ein
// milchiger Klotz auf dem Boden, ohne Kante und ohne Leben. Hier wird der
// Rahmen prozedural aus den UVs gezeichnet — Eckklammern statt durchgehendem
// Rahmen lesen sich auf einem Tile-Grid deutlich klarer, weil die Kanten der
// Nachbar-Tiles nicht mit dem Indikator verschwimmen.
//
// Unlit mit Absicht: der Indikator ist UI in der Welt, der soll nicht von
// Tageszeit, Fog oder Schatten beeinflusst werden.
Shader "CozyCrops/TileIndicator"
{
    Properties
    {
        [MainColor] _BaseColor      ("Farbe", Color) = (1, 1, 1, 0.9)

        [Header(Rahmen)]
        _BorderWidth   ("Rahmenbreite", Range(0.01, 0.5)) = 0.15
        _CornerLength  ("Eckenlaenge (1 = geschlossener Rahmen)", Range(0.05, 1.0)) = 0.34
        _CornerRadius  ("Eckenrundung", Range(0.0, 0.5)) = 0.12
        _EdgeSoftness  ("Kantenweichheit", Range(0.001, 0.15)) = 0.02

        [Header(Fuellung)]
        _FillAlpha     ("Fuellung Alpha", Range(0.0, 1.0)) = 0.14

        [Header(Puls)]
        _PulseSpeed    ("Puls Tempo", Range(0.0, 6.0)) = 1.6
        _PulseAmount   ("Puls Staerke", Range(0.0, 1.0)) = 0.28

        [Header(Fortschritt)]
        // 1 = voller Rahmen. Darunter wird der Rahmen im Uhrzeigersinn ab oben
        // abgeschnitten — damit wird derselbe Indikator zum Fortschrittsring
        // fuer laufende Tool-Aktionen, ohne zweites Prefab.
        _Fill          ("Fortschritt", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            // Transparent+10: soll über dem Boden und über den Pflanzen-Billboards liegen
            "Queue"           = "Transparent+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TileIndicatorForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // LEqual statt Always: der Indikator soll hinter Gebäuden/Bäumen verschwinden,
            // sonst schwebt er sichtbar durch die Scheune.
            ZTest LEqual
            Cull Off

            // Tiefen-Bias Richtung Kamera. Ohne das braucht der Indikator einen echten
            // Höhenabstand zum Boden, um nicht im Tile zu versinken — und der ist bei der
            // isometrischen Kamera als Versatz sichtbar (das Overlay "klebt" nicht mehr auf
            // dem Feld, sondern schwebt daneben). Der Bias verschiebt nur den Tiefenwert,
            // nicht die Geometrie: der Indikator liegt damit optisch exakt auf der Oberfläche
            // und gewinnt trotzdem den Tiefentest.
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _BorderWidth;
                float  _CornerLength;
                float  _CornerRadius;
                float  _EdgeSoftness;
                float  _FillAlpha;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _Fill;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // q: 0 in der Tile-Mitte, 1 an den Kanten — beide Achsen gespiegelt,
                // dadurch reicht die Logik für einen Quadranten und gilt für alle vier.
                float2 q = abs(IN.uv - 0.5) * 2.0;

                // Signed Distance zu einem Rechteck mit runden Ecken.
                // Negativ innerhalb, 0 auf der Kante.
                float  r    = _CornerRadius;
                float2 d2   = q - (1.0 - r);
                float  dist = length(max(d2, 0.0)) + min(max(d2.x, d2.y), 0.0) - r;

                // Ring = zwischen Außenkante und Außenkante minus Rahmenbreite
                float outer = 1.0 - smoothstep(-_EdgeSoftness, _EdgeSoftness, dist);
                float inner = 1.0 - smoothstep(-_BorderWidth - _EdgeSoftness,
                                               -_BorderWidth + _EdgeSoftness, dist);
                float ring  = saturate(outer - inner);

                // Eckklammern: auf dem Rand ist die "Lauf-Koordinate" entlang der Kante
                // immer die kleinere der beiden Achsen (die größere ist ja ~1).
                // Nahe einer Ecke sind beide groß -> Klammer sichtbar.
                float along    = min(q.x, q.y);
                float cornerLo = 1.0 - _CornerLength;
                float corner   = smoothstep(cornerLo - _EdgeSoftness * 2.0,
                                            cornerLo + _EdgeSoftness * 2.0, along);

                // Puls nur auf dem Rahmen, nicht auf der Füllung — sonst flackert
                // die ganze Fläche und wird unruhig.
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed * 6.2831853) * _PulseAmount * 0.5;

                // Fortschritts-Maske: Winkel ab "oben" im Uhrzeigersinn, 0..1.
                // atan2(x, y) statt (y, x) legt die 0 auf +Y und dreht im Uhrzeigersinn —
                // genau die Leserichtung, die man von einer Ladeanzeige erwartet.
                float2 dir     = IN.uv - 0.5;
                float  angle   = frac(atan2(dir.x, dir.y) / 6.2831853 + 1.0);
                // Bei _Fill >= 1 komplett offen lassen, sonst kappt die Maske durch
                // Rundungsfehler den letzten Pixel des vollen Rahmens.
                float  fillMask = _Fill >= 0.999 ? 1.0 : step(angle, _Fill);

                float alpha = saturate(ring * corner * pulse * fillMask + outer * _FillAlpha);

                return half4(_BaseColor.rgb, alpha * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
