// Agua del mapa grande de Vhaldren (pipeline integrado).
// Transparente, con dos mapas normales que se desplazan en direcciones distintas,
// reflejo del cielo por fresnel y un leve oleaje en los vértices.
Shader "Vhaldren/Agua"
{
    Properties
    {
        _ColorSomero ("Color somero", Color) = (0.12, 0.30, 0.32, 0.55)
        _ColorProfundo ("Color profundo", Color) = (0.02, 0.08, 0.12, 0.88)
        _ColorCielo ("Reflejo del cielo", Color) = (0.45, 0.55, 0.65, 1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _Tile ("Metros por repetición", Float) = 14
        _Velocidad ("Velocidad", Float) = 0.03
        _Flujo ("Dirección de corriente (xz)", Vector) = (0, 0, 0, 0)
        _Glossiness ("Brillo", Range(0, 1)) = 0.92
        _Oleaje ("Oleaje", Range(0, 0.5)) = 0.06
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert nolightmap
        #pragma target 3.0

        sampler2D _BumpMap;
        fixed4 _ColorSomero, _ColorProfundo, _ColorCielo;
        float _Tile, _Velocidad, _Oleaje;
        float4 _Flujo;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
            float4 color : COLOR;
        };

        void vert(inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y;
            v.vertex.y += (sin(wp.x * 0.35 + t * 1.3) + cos(wp.z * 0.29 + t * 1.1)) * _Oleaje;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float t = _Time.y * _Velocidad;
            float2 uv = IN.worldPos.xz / max(_Tile, 0.1);
            float2 flujo = _Flujo.xy * _Time.y;
            float3 n1 = UnpackNormal(tex2D(_BumpMap, uv + float2(t, t * 0.6) + flujo));
            float3 n2 = UnpackNormal(tex2D(_BumpMap, uv * 1.7 + float2(-t * 0.8, t) + flujo * 1.3));
            o.Normal = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
            // vertex color alpha = profundidad relativa (0 orilla, 1 hondo) si la malla lo trae
            float hondo = saturate(IN.color.a);
            fixed4 c = lerp(_ColorSomero, _ColorProfundo, hondo);
            float fres = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), 3.0);
            o.Albedo = lerp(c.rgb, _ColorCielo.rgb, fres * 0.55);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = saturate(c.a + fres * 0.25);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
