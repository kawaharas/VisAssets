Shader "Unlit/SliceShader"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

	SubShader
	{
		Tags {
//			"RenderType" = "Opaque"
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
		}
		LOD 100
		Cull Off

		Pass
		{
			Lighting Off
			SetTexture[_MainTex] { combine texture }
		}
	}
}
