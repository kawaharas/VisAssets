Shader "VisAssets/SliceShaderUnlitCutout"
{
	Properties
	{
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
		_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
	}

	SubShader
	{
		Tags {
			"RenderType" = "TransparentCutout"
			"Queue" = "AlphaTest"
			"IgnoreProjector" = "True"
		}
		LOD 100
		Cull Off
		Lighting Off
		ZWrite On

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _Cutoff;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o, o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);

				clip(col.a - _Cutoff);

				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}

		Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f
			{
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;

            v2f vert(appdata_base v)
            {
                v2f o;
                // Unity標準のシャドウキャスター用頂点計算
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                // 透明部分は深度にも書き込まない
                clip(col.a - _Cutoff);
                // 深度バッファへの確実な書き込み
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
	}

	FallBack "Unlit/Transparent Cutout"
}