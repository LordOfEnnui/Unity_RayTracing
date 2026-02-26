//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef RAYTRACESKYMANAGER_CS_HLSL
#define RAYTRACESKYMANAGER_CS_HLSL
// Generated from RayTraceSkyData
// PackingRules = Exact
struct RayTraceSkyData
{
    float4 GroundColor; // x: r y: g z: b w: a 
    float4 SkyColorHorizon; // x: r y: g z: b w: a 
    float4 SkyColorZenith; // x: r y: g z: b w: a 
    float3 SunLightDirection;
    int SunFocus;
    float SunIntensity;
};


#endif
