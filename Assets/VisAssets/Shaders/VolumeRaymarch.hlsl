#ifndef VISASSETS_VOLUME_RAYMARCH_INCLUDED
#define VISASSETS_VOLUME_RAYMARCH_INCLUDED

void VolumeRaymarch_float(
    float4 ScreenPos,
    float3 LocalPos,
    float3 LocalCamPos,
    float3 WorldPos,
    UnityTexture3D VolumeTex, UnitySamplerState SamplerVolumeTex,
    UnityTexture2D TransferTex, UnitySamplerState SamplerTransferTex,
    UnityTexture2D LutTexX, UnitySamplerState SamplerLutTexX,
    UnityTexture2D LutTexY, UnitySamplerState SamplerLutTexY,
    UnityTexture2D LutTexZ, UnitySamplerState SamplerLutTexZ,
    float Density,
    float NumSteps,
    float Threshold,
    float UseLUT,
    float3 ClipPlanePos,
    float3 ClipPlaneNormal,
    float EnableClipping,
    float EnableLighting,
    float Ambient,
    float Diffuse,
    out float4 OutColor
)
{
    OutColor = float4(0, 0, 0, 0);

    // 1. Depth Check (Early Ray Termination for URP)
    float2 uv = ScreenPos.xy / (ScreenPos.w + 1e-6);
    float sceneDepth = SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv);
    float sceneEyeZ = LinearEyeDepth(sceneDepth, _ZBufferParams);

    float3 rayDirLocal = normalize(LocalPos - LocalCamPos);
    float3 invDir = 1.0 / (rayDirLocal + 1e-6);

    // 2. Bounding Box Intersection
    float3 t0 = (0.0 - LocalCamPos) * invDir;
    float3 t1 = (1.0 - LocalCamPos) * invDir;
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);
    float tEnter = max(max(tmin.x, tmin.y), tmin.z);
    float tExit = min(min(tmax.x, tmax.y), tmax.z);

    // 3. Clipping Plane Logic
    if (EnableClipping > 0.5)
    {
        float denom = dot(rayDirLocal, ClipPlaneNormal);
        float tPlane = dot(ClipPlanePos - LocalCamPos, ClipPlaneNormal) / (denom + 1e-6);
        if (denom < -0.0001) tEnter = max(tEnter, tPlane);
        else if (denom > 0.0001) tExit = min(tExit, tPlane);
        else if (dot(LocalCamPos - ClipPlanePos, ClipPlaneNormal) < 0) tEnter = tExit + 1.0;
    }

    tEnter = max(0.0, tEnter);
    if (tEnter >= tExit) return;

    // 4. Restrict tExit by URP Scene Depth
    float3 camPosWorld = _WorldSpaceCameraPos.xyz;
    float3 worldRayDir = normalize(WorldPos - camPosWorld);
    float3 viewRayDir = TransformWorldToView(worldRayDir);
    float rayZRate = -viewRayDir.z;
    if (rayZRate > 0.0001)
    {
        float distWorld = sceneEyeZ / rayZRate;
        float3 hitWorld = camPosWorld + worldRayDir * distWorld;
        float3 hitLocal = TransformWorldToObject(hitWorld) + float3(0.5, 0.5, 0.5);
        float tScene = distance(LocalCamPos, hitLocal);
        tExit = min(tExit, tScene - 0.002);
    }

    // 5. Raymarching Loop
    float stepSize = 1.0 / (NumSteps + 1e-6);
    float4 finalColor = float4(0, 0, 0, 0);
    float currentT = tEnter;

    for (int s = 0; s < int(NumSteps); s++)
    {
        if (currentT >= tExit || finalColor.a >= 0.99) break;

        float3 rayPos = LocalCamPos + rayDirLocal * currentT;
        float3 sampleUV = rayPos;

        if (UseLUT > 0.5)
        {
            sampleUV.x = LutTexX.SampleLevel(SamplerLutTexX, float2(rayPos.x, 0.5), 0).r;
            sampleUV.y = LutTexY.SampleLevel(SamplerLutTexY, float2(rayPos.y, 0.5), 0).r;
            sampleUV.z = LutTexZ.SampleLevel(SamplerLutTexZ, float2(rayPos.z, 0.5), 0).r;
        }

        float pixelVal = VolumeTex.SampleLevel(SamplerVolumeTex, sampleUV, 0).r;
        if (pixelVal > Threshold)
        {
            float4 tfColor = TransferTex.SampleLevel(SamplerTransferTex, float2(pixelVal, 0.5), 0);

            if (EnableLighting > 0.5)
            {
                float d = 0.005;
                float3 vOffset = float3(d, 0, 0);
                float vX1 = VolumeTex.SampleLevel(SamplerVolumeTex, sampleUV + vOffset.xyy, 0).r;
                float vX2 = VolumeTex.SampleLevel(SamplerVolumeTex, sampleUV - vOffset.xyy, 0).r;
                float vY1 = VolumeTex.SampleLevel(SamplerVolumeTex, sampleUV + vOffset.yxy, 0).r;
                float vY2 = VolumeTex.SampleLevel(SamplerVolumeTex, sampleUV - vOffset.yxy, 0).r;
                float vZ1 = VolumeTex.SampleLevel(SamplerVolumeTex, sampleUV + vOffset.yyx, 0).r;
                float vZ2 = VolumeTex.SampleLevel(SamplerVolumeTex, sampleUV - vOffset.yyx, 0).r;

                float3 grad = float3(vX1 - vX2, vY1 - vY2, vZ1 - vZ2);
                float3 normal = -normalize(grad + 1e-5);
                float3 lightDir = -rayDirLocal;
                float diff = max(0.0, dot(normal, lightDir));
                tfColor.rgb *= (Ambient + diff * Diffuse);
            }

            float alpha = saturate(tfColor.a * stepSize * Density);
            finalColor.rgb += (1.0 - finalColor.a) * tfColor.rgb * alpha;
            finalColor.a += (1.0 - finalColor.a) * alpha;
        }
        currentT += stepSize;
    }

    OutColor = finalColor;
}

#endif