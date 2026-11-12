// Efectos de Vhaldren (pipeline integrado): particulas, estelas, rayos y destellos.
// Forma procedural (textura generada en codigo) x color de vertice x tinte.
// La mezcla la elige cada material: aditiva para la luz (brasas, chispas, rayos,
// destellos) o alfa para lo que tapa (humo de sombra, gotas de sangre, polvo).
// Esta en Resources para que el ejecutable lo incluya aunque ningun material
// guardado lo use: los materiales se crean en tiempo de juego.
Shader "Vhaldren/VfxParticula"
{
    Properties
    {
        _MainTex ("Forma", 2D) = "white" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _Intensity ("Intensidad", Range(0, 8)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Mezcla origen", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Mezcla destino", Float) = 1
        // Siempre (8) para brillos que deben verse aunque los tape el personaje.
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Prueba de profundidad", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest [_ZTest]
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= _Intensity;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
