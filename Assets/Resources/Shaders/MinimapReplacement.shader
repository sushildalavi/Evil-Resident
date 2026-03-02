// Valorant-style minimap: clear walls (light gray) and floor (dark). No lighting.
// Use with Camera.RenderWithShader(shader, "RenderType");
Shader "Minimap/Replacement"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #include "UnityCG.cginc"
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float isFloor = step(0.85, i.worldNormal.y);
                fixed3 floorColor = fixed3(0.18, 0.20, 0.24);
                fixed3 wallColor = fixed3(0.52, 0.52, 0.55);
                fixed3 col = lerp(wallColor, floorColor, isFloor);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
