Shader "Common/FriendHpText" {

    // 关闭深度检测，渲染队列设置为最高
    Properties {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _fillCount("FillCount", Range(0,1)) = 1.0
    }
    SubShader
    {
    	Tags { "Queue"="Overlay+1" "RenderType"="Transparent" }

        Cull Off
        ZTest off
        Blend One OneMinusSrcAlpha

        Pass {
            CGPROGRAM

            #pragma vertex  vert
            #pragma fragment frag

            #pragma target 2.0

            #include "UnityCG.cginc"

            struct a2v{
                float4 vertex: POSITION;
                float2 texcoord : TEXCOORD0;

            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _fillCount;

            v2f vert(a2v v){
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }


            fixed4 frag(v2f o) : SV_Target
            {
                fixed4 c =  tex2D (_MainTex, o.uv) * _Color;
                c.rgb *= c.a;
                if (o.uv.x > _fillCount)
                {
                    discard;
                }
                return c;
            }
            ENDCG
        }

    }

   FallBack "VertexLit"
}