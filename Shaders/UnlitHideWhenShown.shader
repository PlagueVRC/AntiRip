Shader "GeoTetra/UnlitHideWhenShown"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"  "VRCFallback" = "Unlit" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = 0. / 0.;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
