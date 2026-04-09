Shader "VisAssets/VolumeRenderer"
{
    Properties
    {
        _VolumeTex ("Volume Texture (3D)", 3D) = "" {}
        _TransferTex ("Transfer Function (1D)", 2D) = "white" {}
        _Density ("Density", Range(0, 100)) = 10.0
        _NumSteps ("Number of Steps", Range(32, 256)) = 128
        _Threshold ("Threshold", Range(0, 1)) = 0.1

        _LutTexX ("LUT X", 2D) = "white" {}
        _LutTexY ("LUT Y", 2D) = "white" {}
        _LutTexZ ("LUT Z", 2D) = "white" {}
        [Toggle] _UseLUT ("Use LUT (Rectilinear)", Float) = 0

        [HideInInspector] _ClipPlanePos ("Clip Plane Position", Vector) = (0,0,0,0)
        [HideInInspector] _ClipPlaneNormal ("Clip Plane Normal", Vector) = (0,1,0,0)
        [HideInInspector] _EnableClipping ("Enable Clipping", Float) = 0

        [HideInInspector] _EnableLighting ("Enable Lighting", Float) = 0
        [HideInInspector] _Ambient ("Ambient", Range(0, 1)) = 0.5
        [HideInInspector] _Diffuse ("Diffuse", Range(0, 2)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 localPos : TEXCOORD0;
                float3 localCamPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler3D _VolumeTex; sampler2D _TransferTex; UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float _Density; int _NumSteps; float _Threshold;
            sampler2D _LutTexX; sampler2D _LutTexY; sampler2D _LutTexZ; float _UseLUT;
            float3 _ClipPlanePos; float3 _ClipPlaneNormal; float _EnableClipping;

            float _EnableLighting;
            float _Ambient; float _Diffuse;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.localPos = v.vertex.xyz + float3(0.5, 0.5, 0.5);

                float3 trueCamPosWorld = mul(unity_CameraToWorld, float4(0, 0, 0, 1)).xyz;
                float3 camPosLocal = mul(unity_WorldToObject, float4(trueCamPosWorld, 1.0)).xyz;

                o.localCamPos = camPosLocal + float3(0.5, 0.5, 0.5);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float depthRaw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);
                float sceneEyeZ = LinearEyeDepth(depthRaw);

                if (sceneEyeZ > _ProjectionParams.z - 1.0) sceneEyeZ = 99999.0;

                float3 rayDirLocal = normalize(i.localPos - i.localCamPos);
                float3 invDir = 1.0 / rayDirLocal;

                float3 t0 = (0.0 - i.localCamPos) * invDir;
                float3 t1 = (1.0 - i.localCamPos) * invDir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                float tEnter = max(max(tmin.x, tmin.y), tmin.z);
                float tExit = min(min(tmax.x, tmax.y), tmax.z);

                if (_EnableClipping > 0.5)
                {
                    float denom = dot(rayDirLocal, _ClipPlaneNormal);
                    float tPlane = dot(_ClipPlanePos - i.localCamPos, _ClipPlaneNormal) / denom;

                    if (denom < -0.0001) tEnter = max(tEnter, tPlane);
                    else if (denom > 0.0001) tExit = min(tExit, tPlane);
                    else if (dot(i.localCamPos - _ClipPlanePos, _ClipPlaneNormal) < 0) tEnter = tExit + 1.0;
                }

                tEnter = max(0.0, tEnter);
                if (tEnter >= tExit) return float4(0,0,0,0);

                float3 trueCamPosWorld = mul(unity_CameraToWorld, float4(0, 0, 0, 1)).xyz;
                float3 worldRayDir = normalize(i.worldPos - trueCamPosWorld);
                float3 viewRayDir = mul((float3x3)UNITY_MATRIX_V, worldRayDir);
                float rayZRate = -viewRayDir.z;

                if (rayZRate > 0.0001)
                {
                    float distWorld = sceneEyeZ / rayZRate;
                    float3 hitWorld = trueCamPosWorld + worldRayDir * distWorld;
                    float3 hitLocal = mul(unity_WorldToObject, float4(hitWorld, 1.0)).xyz + float3(0.5, 0.5, 0.5);
                    float tScene = distance(i.localCamPos, hitLocal);
                    tExit = min(tExit, tScene - 0.002);
                }

                float stepSize = 1.0 / _NumSteps;
                float4 finalColor = float4(0, 0, 0, 0);
                float currentT = tEnter;

                [loop]
                for (int s = 0; s < 1024; s++)
                {
                    if (currentT >= tExit || finalColor.a >= 0.99) break;

                    float3 rayPos = i.localCamPos + rayDirLocal * currentT;
                    float3 sampleUV = rayPos;

                    if (_UseLUT > 0.5)
                    {
                        sampleUV.x = tex2Dlod(_LutTexX, float4(rayPos.x, 0.0, 0.0, 0.0)).r;
                        sampleUV.y = tex2Dlod(_LutTexY, float4(rayPos.y, 0.0, 0.0, 0.0)).r;
                        sampleUV.z = tex2Dlod(_LutTexZ, float4(rayPos.z, 0.0, 0.0, 0.0)).r;
                    }

                    float pixelVal = tex3Dlod(_VolumeTex, float4(sampleUV, 0.0)).r;

                    if (pixelVal > _Threshold)
                    {
                        float4 tfColor = tex2Dlod(_TransferTex, float4(pixelVal, 0.5, 0, 0));

                        // Volume Shading Calculation
                        if (_EnableLighting > 0.5)
                        {
                            // Calculate gradient (density variation) by sampling surrounding 6 directions
                            float d = 0.005;
                            float vX1 = tex3Dlod(_VolumeTex, float4(sampleUV + float3(d, 0, 0), 0.0)).r;
                            float vX2 = tex3Dlod(_VolumeTex, float4(sampleUV - float3(d, 0, 0), 0.0)).r;
                            float vY1 = tex3Dlod(_VolumeTex, float4(sampleUV + float3(0, d, 0), 0.0)).r;
                            float vY2 = tex3Dlod(_VolumeTex, float4(sampleUV - float3(0, d, 0), 0.0)).r;
                            float vZ1 = tex3Dlod(_VolumeTex, float4(sampleUV + float3(0, 0, d), 0.0)).r;
                            float vZ2 = tex3Dlod(_VolumeTex, float4(sampleUV - float3(0, 0, d), 0.0)).r;

                            float3 grad = float3(vX1 - vX2, vY1 - vY2, vZ1 - vZ2);

                            // Normal vector based on the reverse density gradient
                            float3 normal = -normalize(grad + float3(0.00001, 0.00001, 0.00001));

                            // Headlight model (Light source from viewer)
                            float3 lightDir = -rayDirLocal;

                            // Lambertian diffuse reflection
                            float diff = max(0.0, dot(normal, lightDir));

                            // Ambient light + Diffuse reflection
                            tfColor.rgb = tfColor.rgb * (_Ambient + diff * _Diffuse);
                        }

                        float alpha = saturate(tfColor.a * stepSize * _Density);
                        finalColor.rgb += (1.0 - finalColor.a) * tfColor.rgb * alpha;
                        finalColor.a += (1.0 - finalColor.a) * alpha;
                    }
                    currentT += stepSize;
                }
                return finalColor;
            }
            ENDCG
        }
    }
}