Shader "VolumeRendering/VolumeRendering"
{

	Properties
	{
		_Volume("Volume", 3D) = "" {}
		_Color("Color", Color) = (1, 1, 1, 1)
		_Iteration("Iteration", Int) = 10
		_Intensity("Intensity", Range(0, 1)) = 0.1
	}

	CGINCLUDE

	#include "UnityCG.cginc"

	struct appdata
	{
		float4 vertex : POSITION;
	};

	struct v2f
	{
		float4 vertex   : SV_POSITION;
		float4 localPos : TEXCOORD0;
		float4 worldPos : TEXCOORD1;
	};

	sampler3D _Volume;
	fixed4 _Color;
	int _Iteration;
	fixed _Intensity;

	v2f vert(appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.localPos = v.vertex;
		o.worldPos = mul(unity_ObjectToWorld, v.vertex);
		return o;
	}

	fixed4 frag(v2f i) : SV_Target
	{
		float3 wdir = i.worldPos - _WorldSpaceCameraPos;
		float3 ldir = normalize(mul(unity_WorldToObject, wdir));
		float3 lstep = ldir / _Iteration;
		float3 lpos = i.localPos;
		fixed output = 0.0;

		[loop]
		for (int i = 0; i < _Iteration; ++i)
		{
			fixed a = tex3D(_Volume, lpos + 0.5).r;
			output += (1 - output) * a * _Intensity;
			lpos += lstep;
			if (!all(max(0.5 - abs(lpos), 0.0))) break;
		}

		return _Color * output;
	}

	ENDCG

	SubShader
	{

		Tags
		{ 
			"Queue" = "Transparent"
			"RenderType" = "Transparent" 
		}

		Pass
		{
			Cull Back
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha 
			Lighting Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}

	}

}
