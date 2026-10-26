Shader "GesturalBoids/FacetedWhite"
{
    Properties { _Integration ("Shadow integration", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Integration;
            struct Output { float4 position : SV_POSITION; float3 normal : TEXCOORD0; };
            Output vert(float4 vertex : POSITION, float3 normal : NORMAL)
            {
                Output o; o.position = UnityObjectToClipPos(vertex); o.normal = UnityObjectToWorldNormal(normal); return o;
            }
            fixed4 frag(Output i) : SV_Target
            {
                float light = 0.48 + 0.52 * saturate(dot(normalize(i.normal), normalize(float3(-0.5, 0.8, -1))));
                float surface = lerp(0.3, 1, _Integration);
                float shade = lerp(light * surface, 1, _Integration * _Integration * 0.85);
                return fixed4(shade, shade, shade, 1);
            }
            ENDCG
        }
    }
}
