Shader "Custom/SurfaceShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }

	SubShader
    {
        Tags
		{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
		}
		Cull Off
		LOD 200

        CGPROGRAM

        #pragma surface surf Lambert alpha
        #pragma target 3.0

        struct Input
        {
            float4 color: COLOR;
        };

		fixed4 _Color;

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color;
            o.Alpha = IN.color.a;
        }

		ENDCG
	}
	FallBack "Diffuse"
}
