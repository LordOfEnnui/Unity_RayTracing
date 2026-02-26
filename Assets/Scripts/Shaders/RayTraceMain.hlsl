#ifndef RAY_TRACE_MAIN
#define RAY_TRACE_MAIN
#include "RayTraceCollision.hlsl"
#include "RayTraceRandom.hlsl"
#include "../RayTraceSkyManager.cs.hlsl"

StructuredBuffer<RayTraceSkyData> _SkyDataBuffer;

float3 GetEnvironmentLight(Ray ray) {
    RayTraceSkyData skyData = _SkyDataBuffer[0];
    float skyGradientT = pow(smoothstep(0, 0.4, ray.dir.y), 0.35);
    float3 skyGradient = lerp(skyData.SkyColorHorizon, skyData.SkyColorZenith, skyGradientT).rgb;
    float sun = pow(max(0, dot(ray.dir, -skyData.SunLightDirection)), skyData.SunFocus) * skyData.SunIntensity;
    
    float groundToSkyT = smoothstep(-0.01, 0, ray.dir.y);
    float sunMask = groundToSkyT >= 1;
    return lerp(skyData.GroundColor.rgb, skyGradient, groundToSkyT) + sun * sunMask;
}

float3 Trace(Ray ray, int maxBounceCount, inout uint rngState, inout int2 stats, out float3 normal) {
    
    float3 incomingLight = 0;
    float3 rayColor = 1;
    
    for (int i = 0; i <= maxBounceCount; i++) {
        HitInfo hitInfo = CalculateRayCollision(ray, stats);
        
        
        if (hitInfo.didHit) {
            ray.origin = hitInfo.hitPoint;
            RayTracingMaterial material = hitInfo.material;
            bool isSpecularBounce = material.specularProbability >= RandomValue(rngState);
            
            if (i == 0) {
                normal = hitInfo.normal;
            }
            
            float3 diffuseDir = normalize(hitInfo.normal + RandomDirection(rngState));
            float3 specularDir = reflect(ray.dir, hitInfo.normal);

            ray.dir = normalize(lerp(diffuseDir, specularDir, material.smoothness * isSpecularBounce));
            
            float3 emittedLight = material.emissionColor.rgb * material.emissionStrength;
            incomingLight += emittedLight * rayColor;
            rayColor *= lerp(material.color, material.specularColor, isSpecularBounce).rgb;
        } else {
            incomingLight += GetEnvironmentLight(ray) * rayColor;
            break;
        }
    }
    return incomingLight;
}

#endif