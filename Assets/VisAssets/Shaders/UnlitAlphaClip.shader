Shader "VisAssets/UnlitAlphaClip"
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
				UNITY_VERTEX_INPUT_INSTANCE_ID // XR support: Add Instance ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO // XR support: Add stereo output variable
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _Cutoff;

			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v); // XR support: Setup Instance ID
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // XR support: Initialize stereo output

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o, o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); // XR support: Initialize stereo eye settings in fragment shader

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

            // Define a custom struct instead of appdata_base for XR support
            struct appdata_shadow
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // XR support
            };

            struct v2f
			{
                V2F_SHADOW_CASTER;
				float2 uv : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO // XR support
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;

			v2f vert(appdata_shadow v)
            {
                v2f o;
				UNITY_SETUP_INSTANCE_ID(v); // XR support
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // XR support

                // Unity's standard shadow caster vertex calculation
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
            }

            float4 frag(v2f i) : SV_Target
            {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); // XR support

                fixed4 col = tex2D(_MainTex, i.uv);

                // Do not write transparent areas to the depth buffer
                clip(col.a - _Cutoff);

                // Ensure writing to the depth buffer
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
	}

	FallBack "Unlit/Transparent Cutout"
}