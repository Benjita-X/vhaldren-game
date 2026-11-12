// Shader triplanar del mundo de Vhaldren (pipeline integrado).
// Proyecta la textura segun la posicion en el mundo, de modo que el suelo de 92 m,
// un muro largo y un bloque pequeño comparten la misma escala sin estirarse.
// Mezcla los tres ejes por la normal y añade relieve con el mapa normal.
Shader "Vhaldren/Triplanar"
{
    Properties
    {
        _MainTex ("Color", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _Tile ("Metros por repeticion", Float) = 4
        _BumpScale ("Relieve", Range(0, 2)) = 1
        _Glossiness ("Suavidad", Range(0, 1)) = 0.12
        _Sharpness ("Nitidez de mezcla", Range(1, 8)) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 250

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        float _Tile;
        float _BumpScale;
        half _Glossiness;
        float _Sharpness;

        struct Input
        {
            float3 worldPosition;
            float3 objectNormalWorld;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.objectNormalWorld = UnityObjectToWorldNormal(v.normal);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.objectNormalWorld);
            float3 weights = pow(abs(n), _Sharpness);
            weights /= max(dot(weights, float3(1, 1, 1)), 0.0001);

            float scale = 1.0 / max(_Tile, 0.05);
            float2 uvX = IN.worldPosition.zy * scale;
            float2 uvY = IN.worldPosition.xz * scale;
            float2 uvZ = IN.worldPosition.xy * scale;

            fixed4 color = tex2D(_MainTex, uvX) * weights.x +
                           tex2D(_MainTex, uvY) * weights.y +
                           tex2D(_MainTex, uvZ) * weights.z;

            float3 nX = UnpackScaleNormal(tex2D(_BumpMap, uvX), _BumpScale);
            float3 nY = UnpackScaleNormal(tex2D(_BumpMap, uvY), _BumpScale);
            float3 nZ = UnpackScaleNormal(tex2D(_BumpMap, uvZ), _BumpScale);
            // Aproximacion habitual: mezclar los relieves en espacio tangente.
            o.Normal = normalize(nX * weights.x + nY * weights.y + nZ * weights.z);

            o.Albedo = color.rgb * _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
