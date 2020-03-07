Shader "Shaders/Common/BumpedSpecular_NoNormal" {

	//高光 （不带法线贴图）
	Properties {
		_Color ("Color Tint", Color) = (1, 1, 1, 1)
		_MainTex ("Main Tex", 2D) = "white" {}

		_Specular ("Specular Color", Color) = (1, 1, 1, 1)
		_Gloss ("Gloss", Range(8.0, 256)) = 20
	}
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry"}

		Pass {
			Tags { "LightMode"="ForwardBase" }

			CGPROGRAM

			#pragma multi_compile_fwdbase

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"

			fixed4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Specular;
			float _Gloss;

			struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float4 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float3 worldNormal : TEXCOORD2;
				float3 worldViewDir : TEXCOORD3;
				SHADOW_COORDS(4)
			};

			v2f vert(a2v v) {
			 	v2f o;
			 	o.pos = UnityObjectToClipPos(v.vertex);

			 	o.uv.xy = v.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;

				o.worldNormal = UnityObjectToWorldNormal(v.normal);

                float3 worldPos = mul((float3x3)unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos;
                o.worldViewDir = UnityWorldSpaceViewDir(worldPos);

  				//使用TRANSFER_SHADOW 注意：
					// 1 必须保证a2v中顶点坐标名为vertex
					// 2 顶点着色器的输入形参名必须为v
					// 3 v2f的顶点变量名必须为pos

					//总结下：a2v中必须要有vertex表示顶点位置 v2f中必须有pos表是裁剪空间的位置 形参必须得是v
  				TRANSFER_SHADOW(o);

			 	return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				float3 worldPos = i.worldPos;
				fixed3 worldLightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                fixed3 worldNormal = normalize(i.worldNormal);
                fixed3 worldViewDir = normalize(i.worldViewDir);

				fixed3 albedo = tex2D(_MainTex, i.uv.xy).rgb * _Color.rgb;

				fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * albedo;

			 	fixed3 diffuse = _LightColor0.rgb * albedo * max(0, dot(worldNormal, worldLightDir));

			 	fixed3 halfDir = normalize(worldLightDir + worldViewDir);
			 	fixed3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0, dot(worldNormal, halfDir)), _Gloss);

				UNITY_LIGHT_ATTENUATION(atten, i, worldPos);

				return fixed4(ambient + (diffuse + specular) * atten, 1.0);
			}

			ENDCG
		}

		Pass {
			Tags { "LightMode"="ForwardAdd" }

			Blend One One

			CGPROGRAM

			#pragma multi_compile_fwdadd
			// Use the line below to add shadows for point and spot lights
//			#pragma multi_compile_fwdadd_fullshadows

			#pragma vertex vert
			#pragma fragment frag

			#include "Lighting.cginc"
			#include "AutoLight.cginc"

			fixed4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;

			fixed4 _Specular;
			float _Gloss;

			struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float4 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float3 worldNormal : TEXCOORD2;
				float3 worldViewDir : TEXCOORD3;
				SHADOW_COORDS(4)
			};

			v2f vert(a2v v) {
			 	v2f o;
			 	o.pos = UnityObjectToClipPos(v.vertex);

			 	o.uv.xy = v.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;

				o.worldNormal = UnityObjectToWorldNormal(v.normal);

                float3 worldPos = mul((float3x3)unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos;
                o.worldViewDir = UnityWorldSpaceViewDir(worldPos);



			 	//使用TRANSFER_SHADOW 注意：
					// 1 必须保证a2v中顶点坐标名为vertex
					// 2 顶点着色器的输入形参名必须为v
					// 3 v2f的顶点变量名必须为pos

					//总结下：a2v中必须要有vertex表示顶点位置 v2f中必须有pos表是裁剪空间的位置 形参必须得是v
			 	TRANSFER_SHADOW(o);

			 	return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				float3 worldPos = i.worldPos;
				fixed3 worldLightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                fixed3 worldNormal = normalize(i.worldNormal);
                fixed3 worldViewDir = normalize(i.worldViewDir);


				fixed3 albedo = tex2D(_MainTex, i.uv.xy).rgb * _Color.rgb;

			 	fixed3 diffuse = _LightColor0.rgb * albedo * max(0, dot(worldNormal, worldLightDir));

			 	fixed3 halfDir = normalize(worldLightDir + worldViewDir);
			 	fixed3 specular = _LightColor0.rgb * _Specular.rgb * pow(max(0, dot(worldNormal, halfDir)), _Gloss);

				UNITY_LIGHT_ATTENUATION(atten, i, worldPos);

				return fixed4((diffuse + specular) * atten, 1.0);
			}

			ENDCG
		}
	}
	FallBack "Specular"
}
