// Discos de efectos de Vhaldren (pipeline integrado), dibujados en un quad.
// Un solo shader cubre tres usos segun sus parametros:
//  - aviso de area: tinte del area + borde nitido + relleno que crece hasta el borde;
//  - onda expansiva: solo el frente (_Fill = radio actual), grueso y que se apaga;
//  - marca en el suelo: mancha suave e irregular (quemado, escarcha, sangre).
// La mezcla la elige el material: aditiva (luz) o alfa (manchas que oscurecen).
// Esta en Resources para que el ejecutable lo incluya: los materiales se crean en codigo.
Shader "Vhaldren/VfxSuelo"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Fill ("Llenado (radio 0-1)", Range(0, 1)) = 0
        _Base ("Opacidad del area", Range(0, 1)) = 0.12
        _Soft ("Suavidad del contorno", Range(0.001, 1)) = 0.02
        _RimWidth ("Grosor del borde", Range(0, 0.5)) = 0.05
        _RimAlpha ("Opacidad del borde", Range(0, 1)) = 1
        _FillAlpha ("Opacidad del relleno", Range(0, 1)) = 0.3
        _EdgeWidth ("Grosor del frente", Range(0.001, 0.6)) = 0.08
        _EdgeAlpha ("Opacidad del frente", Range(0, 1)) = 0.9
        _Ticks ("Marcas runicas", Range(0, 1)) = 0
        _Spin ("Giro de las marcas", Float) = 0
        _Noise ("Irregularidad", Range(0, 1)) = 0
        _Seed ("Semilla", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Mezcla origen", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Mezcla destino", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-20" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Lighting Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            half4 _Color;
            float _Fill;
            float _Base;
            float _Soft;
            float _RimWidth;
            float _RimAlpha;
            float _FillAlpha;
            float _EdgeWidth;
            float _EdgeAlpha;
            float _Ticks;
            float _Spin;
            float _Noise;
            float _Seed;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = v.uv * 2.0 - 1.0;   // centro del disco = (0,0), borde = radio 1
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 p = i.p;
                float r = length(p);
                float aa = max(fwidth(r), 0.0015);

                // Contorno irregular (marcas) o circulo limpio (avisos).
                float n = valueNoise(p * 3.1 + _Seed) * 0.65 + valueNoise(p * 7.3 + _Seed * 1.37) * 0.35;
                float outer = 1.0 - _Noise * 0.32 * n;
                float inside = 1.0 - smoothstep(outer - _Soft - aa, outer, r);
                float base = _Base * inside * lerp(1.0, 0.4 + n * 0.95, _Noise);

                // Borde exterior nitido: el limite real del area.
                float rimHalf = _RimWidth * 0.5;
                float rim = _RimAlpha * step(0.0001, _RimWidth) *
                    (1.0 - smoothstep(rimHalf, rimHalf + aa, abs(r - (1.0 - rimHalf))));

                // Relleno que crece desde el centro y su frente luminoso.
                float hasFill = step(0.0001, _Fill);
                float fillMask = 1.0 - smoothstep(_Fill - aa, _Fill + aa, r);
                float fill = _FillAlpha * fillMask * hasFill;
                float edge = _EdgeAlpha * hasFill * fillMask * smoothstep(_Fill - _EdgeWidth, _Fill, r);

                // Marcas runicas: anillo interior discontinuo que gira despacio.
                float angle = atan2(p.y, p.x) * 0.1591549 + 0.5;
                float dash = step(0.45, frac(angle * 28.0 + _Spin));
                float band = 1.0 - smoothstep(0.012, 0.012 + aa, abs(r - 0.86));
                float ticks = _Ticks * dash * band;

                float alpha = saturate(max(base, fill) + rim + edge + ticks) * _Color.a;
                float highlight = saturate(rim * 0.5 + edge * 0.6);
                half3 rgb = lerp(_Color.rgb, half3(1.0, 1.0, 1.0), highlight * 0.35);
                return half4(rgb, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
