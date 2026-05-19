Shader "VisAssets/VolumeRenderer_URP"
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
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "VolumePass"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 localPos : TEXCOORD0;
                float3 localCamPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Uniforms
            TEXTURE3D(_VolumeTex); SAMPLER(sampler_VolumeTex);
            TEXTURE2D(_TransferTex); SAMPLER(sampler_TransferTex);
            TEXTURE2D(_LutTexX); SAMPLER(sampler_LutTexX);
            TEXTURE2D(_LutTexY); SAMPLER(sampler_LutTexY);
            TEXTURE2D(_LutTexZ); SAMPLER(sampler_LutTexZ);

            CBUFFER_START(UnityPerMaterial)
                float _Density;
                int _NumSteps;
                float _Threshold;
                float _UseLUT;
                float3 _ClipPlanePos;
                float3 _ClipPlaneNormal;
                float _EnableClipping;
                float _EnableLighting;
                float _Ambient;
                float _Diffuse;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.localPos = input.positionOS.xyz + float3(0.5, 0.5, 0.5);

                float3 camPosWorld = GetCameraPositionWS();
                float3 camPosLocal = TransformWorldToObject(camPosWorld);
                output.localCamPos = camPosLocal + float3(0.5, 0.5, 0.5);

                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Depth Check for Early Ray Termination
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = SampleSceneDepth(uv);
                float sceneEyeZ = LinearEyeDepth(sceneDepth, _ZBufferParams);

                float3 rayDirLocal = normalize(input.localPos - input.localCamPos);
                float3 invDir = 1.0 / (rayDirLocal + 1e-6);

                float3 t0 = (0.0 - input.localCamPos) * invDir;
                float3 t1 = (1.0 - input.localCamPos) * invDir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                float tEnter = max(max(tmin.x, tmin.y), tmin.z);
                float tExit = min(min(tmax.x, tmax.y), tmax.z);

                // Clipping Plane logic
                if (_EnableClipping > 0.5)
                {
                    float denom = dot(rayDirLocal, _ClipPlaneNormal);
                    float tPlane = dot(_ClipPlanePos - input.localCamPos, _ClipPlaneNormal) / (denom + 1e-6);
                    if (denom < -0.0001) tEnter = max(tEnter, tPlane);
                    else if (denom > 0.0001) tExit = min(tExit, tPlane);
                    else if (dot(input.localCamPos - _ClipPlanePos, _ClipPlaneNormal) < 0) tEnter = tExit + 1.0;
                }

                tEnter = max(0.0, tEnter);
                if (tEnter >= tExit) return 0;

                // Restrict tExit by Scene Depth
                float3 camPosWorld = GetCameraPositionWS();
                float3 worldRayDir = normalize(input.worldPos - camPosWorld);
                float3 viewRayDir = mul((float3x3)GetWorldToViewMatrix(), worldRayDir);
                float rayZRate = -viewRayDir.z;

                if (rayZRate > 0.0001)
                {
                    float distWorld = sceneEyeZ / rayZRate;
                    float3 hitWorld = camPosWorld + worldRayDir * distWorld;
                    float3 hitLocal = TransformWorldToObject(hitWorld) + float3(0.5, 0.5, 0.5);
                    float tScene = distance(input.localCamPos, hitLocal);
                    tExit = min(tExit, tScene - 0.002);
                }

                float stepSize = 1.0 / _NumSteps;
                float4 finalColor = 0;
                float currentT = tEnter;

                for (int s = 0; s < _NumSteps; s++)
                {
                    if (currentT >= tExit || finalColor.a >= 0.99) break;

                    float3 rayPos = input.localCamPos + rayDirLocal * currentT;
                    float3 sampleUV = rayPos;

                    if (_UseLUT > 0.5)
                    {
                        sampleUV.x = SAMPLE_TEXTURE2D_LOD(_LutTexX, sampler_LutTexX, float2(rayPos.x, 0.5), 0).r;
                        sampleUV.y = SAMPLE_TEXTURE2D_LOD(_LutTexY, sampler_LutTexY, float2(rayPos.y, 0.5), 0).r;
                        sampleUV.z = SAMPLE_TEXTURE2D_LOD(_LutTexZ, sampler_LutTexZ, float2(rayPos.z, 0.5), 0).r;
                    }

                    float pixelVal = SAMPLE_TEXTURE3D_LOD(_VolumeTex, sampler_VolumeTex, sampleUV, 0).r;
                    if (pixelVal > _Threshold)
                    {
                        float4 tfColor = SAMPLE_TEXTURE2D_LOD(_TransferTex, sampler_TransferTex, float2(pixelVal, 0.5), 0);

                        if (_EnableLighting > 0.5)
                        {
                            float d = 0.005;
                            float3 vOffset = float3(d, 0, 0);
                            float vX1 = SAMPLE_TEXTURE3D_LOD(_VolumeTex, sampler_VolumeTex, sampleUV + vOffset.xyy, 0).r;
                            float vX2 = SAMPLE_TEXTURE3D_LOD(_VolumeTex, sampler_VolumeTex, sampleUV - vOffset.xyy, 0).r;
                            float vY1 = SAMPLE_TEXTURE3D_LOD(_VolumeTex, sampler_VolumeTex, sampleUV + vOffset.yxy, 0).r;
                            float vY2 = SAMPLE_TEXTURE3D_LOD(_VolumeTex, sampler_VolumeTex, sampleUV - vOffset.yxy, 0).r;
                            float vZ1 = SAMPLE_TEXTURE3D_LOD(_VolumeTex, sampler_VolumeTex, sampleUV + vOffset.yyx, 0).r;
                            float vZ2 = SAMPLE_TEXTURE3D_LOD(_VolumeTex, sampler_VolumeTex, sampleUV - vOffset.yyx, 0).r;

                            float3 grad = float3(vX1 - vX2, vY1 - vY2, vZ1 - vZ2);
                            float3 normal = -normalize(grad + 1e-5);
                            float3 lightDir = -rayDirLocal;
                            float diff = max(0.0, dot(normal, lightDir));
                            tfColor.rgb *= (_Ambient + diff * _Diffuse);
                        }

                        float alpha = saturate(tfColor.a * stepSize * _Density);
                        finalColor.rgb += (1.0 - finalColor.a) * tfColor.rgb * alpha;
                        finalColor.a += (1.0 - finalColor.a) * alpha;
                    }
                    currentT += stepSize;
                }

                return finalColor;
            }
            ENDHLSL
        }
    }
}