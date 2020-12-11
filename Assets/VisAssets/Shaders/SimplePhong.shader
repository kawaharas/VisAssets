Shader "Custom/SimplePhong"
{
	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
		}
		Cull Off
		LOD 200

		CGPROGRAM

		#pragma surface surf SimplePhong
		#pragma target 3.0

		struct Input
		{
			float2 uv_MainTex;
			float4 color: COLOR;
		};

		void surf(Input IN, inout SurfaceOutput  o)
		{
//			o.Albedo = fixed4(1,1,1,1);
			o.Albedo = IN.color;
			o.Alpha  = IN.color.a;
		}

		half4 LightingSimplePhong(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
		{
			 half NdotL = max(0, dot(s.Normal, lightDir));
			 float3 R = normalize(-lightDir + 2.0 * s.Normal * NdotL);
			 float3 spec = pow(max(0, dot(R, viewDir)), 10.0);

			 half4 c;
			 c.rgb = s.Albedo * _LightColor0.rgb * NdotL + spec + fixed4(0.1f, 0.1f, 0.1f, 1);
			 c.a = s.Alpha;
			 return c;
		}

		ENDCG
	}
	FallBack "Diffuse"
}
