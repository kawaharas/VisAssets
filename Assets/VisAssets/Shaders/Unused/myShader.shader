Shader "Myshader/testShader"
{
	SubShader
	{
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float3 normal : NORMAL;
				float3 tangent : TANGENT;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 color  : COLOR;
				float3 normal : NORMAL;
				//fixed4 diff : COLOR1;
			};

			fixed4 _LightColor0;

			void vert (in appdata v, out v2f o)
			{
			    float3 tangent = v.tangent;
			    float3 binormal = normalize(cross(v.normal, tangent));

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				o.normal = v.normal;
				//o.normal = normalize(cross(tangent, binormal));
				//o.diff = max( 0, dot(o.normal, _WorldSpaceLightPos0.xyz)) * _LightColor0;
			}
			
			void frag (in v2f i, out float4 col : SV_Target)
			{
			    //float4 intensity = max( 0, dot(i.normal, _WorldSpaceLightPos0.xyz)) * _LightColor0;
			    float4 intensity = max(0, dot(i.normal, _WorldSpaceLightPos0.xyz));
			    //float4 vv = {1,1,1,1};// red. OK but transparency (4th) is not effective;
			    col = i.color * (intensity + 0.5);
			}
			ENDCG
		}
	}
}