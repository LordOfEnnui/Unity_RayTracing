#pragma editor_sync_compilation
#pragma target 5.0
#include "RayTraceMain.hlsl"

bool _RayTraceActive;
float3 _ViewParams;
float4x4 _CamLocalToWorldMatrix;
int _MaxBounceCount;
int _NumRaysPerPixel;
sampler2D _MainTexOld;
int _NumFramesPassed;
float _ViewPointJitterStrength;
float _OriginJitterStrength;
int _DebugView;
float _DebugViewBoxLimit;
float _DebugViewTriLimit;


Ray CreateRay(float2 uv, inout uint rngState) {
    float3 viewPointLocal = float3(uv - 0.5, 1) * _ViewParams;
    float3 viewPoint = mul(_CamLocalToWorldMatrix, float4(viewPointLocal, 1)).xyz;
    float3 camRight = _CamLocalToWorldMatrix._m00_m10_m20;
    float3 camUp = _CamLocalToWorldMatrix._m01_m11_m21;
    Ray ray;
    float2 defocusJitter = RandomPointInCircle(rngState) * _OriginJitterStrength / _ScreenParams.x;
    ray.origin = _WorldSpaceCameraPos + camRight * defocusJitter.x + camUp * defocusJitter.y;
    float2 jitter = RandomPointInCircle(rngState) * _ViewPointJitterStrength / _ScreenParams.x;
    float3 jitteredViewPoint = viewPoint + camRight * jitter.x + camUp * jitter.y;
    ray.dir = normalize(jitteredViewPoint - ray.origin);
    return ray;
}

float4 RenderFrame(float2 uv, inout int2 stats, out float3 normalDebug) {
    uint state = Random_Seed(uv);
    float3 totalIncomingLight = 0;
    
    for (int rayIndex = 0; rayIndex < _NumRaysPerPixel; rayIndex++) {
        Ray ray = CreateRay(uv, state);
        float3 normal;
        totalIncomingLight += Trace(ray, _MaxBounceCount, state, stats, normal);
        if (rayIndex == 0) {
            normalDebug = normal;
        }
    }
    
    float3 pixelColor = totalIncomingLight / _NumRaysPerPixel;
    return float4(pixelColor, 1);
}

void SGF_RayTracer_float(float2 uv, float4 InColor, out float4 OutColor) {
    if (!_RayTraceActive) {
        OutColor = InColor;
        return;
    }
    int2 stats = 0;
    float3 normal;
    
    float4 oldRender = tex2D(_MainTexOld, float2(uv.x, 1- uv.y));
    float4 newRender = RenderFrame(uv, stats, normal);
    float weight = 1.0 / (_NumFramesPassed + 1);
    float4 output = oldRender * (1 - weight) + newRender * weight;
    OutColor = output;
    
    float boxDebug = stats[0] / _DebugViewBoxLimit;
    float triDebug = stats[1] / _DebugViewTriLimit;
    
    switch (_DebugView) {
        case 0:
            return;
        case 1: 
            OutColor = float4(normal * 0.5 + 0.5, 1);
            return;
        case 2:
            OutColor = boxDebug < 1 ? boxDebug : float4(1, 0, 0, 1);
            return;
        case 3:
            OutColor = triDebug < 1 ? triDebug : float4(1, 0, 0, 1);
            return;
        default: 
            return;
    }
}

void SGF_RayTracer_half(half2 uv, half3 InColor, out half3 OutColor) {
    if (!_RayTraceActive) {
        OutColor = InColor;
        return;
    }
    OutColor = half3(1, 1, 0);
}
