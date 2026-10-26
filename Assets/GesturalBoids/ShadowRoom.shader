Shader "GesturalBoids/ShadowRoom"
{
    Properties
    {
        _Whiten ("White room", Range(0,1)) = 0
        _Glow ("Inner light", Range(0,1)) = 0
        _Center ("Player", Vector) = (0,1.3,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        Pass
        {
            Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Whiten, _Glow; float4 _Center;
            struct Output { float4 pos:SV_POSITION; float3 world:TEXCOORD0; };
            Output vert(float4 vertex:POSITION) { Output o; o.pos=UnityObjectToClipPos(vertex);o.world=mul(unity_ObjectToWorld,vertex).xyz;return o; }
            fixed4 frag(Output i):SV_Target
            {
                float horizon=smoothstep(-0.02,0.06,i.world.y);
                float halo=exp(-dot(i.world.xy-_Center.xy,i.world.xy-_Center.xy)*0.35);
                float shade=lerp(0.16,0.045,horizon)+halo*0.065;
                float glow=exp(-length(i.world.xy-_Center.xy)/(0.15+_Glow*3))*_Glow;
                shade=lerp(saturate(shade+glow),1,_Whiten);
                return fixed4(shade,shade,shade,1);
            }
            ENDCG
        }
    }
}
