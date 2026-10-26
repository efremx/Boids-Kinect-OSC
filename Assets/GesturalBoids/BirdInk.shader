Shader "GesturalBoids/BirdInk"
{
    Properties { _Color ("Ink color", Color) = (1,1,1,1) }
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
            fixed4 _Color;
            struct Output { float4 position : SV_POSITION; float3 normal : TEXCOORD0; };
            Output vert(float4 vertex : POSITION, float3 normal : NORMAL)
            { Output o; o.position = UnityObjectToClipPos(vertex); o.normal = UnityObjectToWorldNormal(normal); return o; }
            fixed4 frag(Output i) : SV_Target
            { float light = 0.55 + 0.45 * abs(dot(normalize(i.normal), normalize(float3(-0.4,0.8,-1)))); return fixed4(_Color.rgb * light, 1); }
            ENDCG
        }
    }
}
