#ifndef RAY_TRACE_RANDOM
#define RAY_TRACE_RANDOM

int _Frame;

uint PixelIndex(float2 uv) {
    uint2 numPixels = _ScreenParams.xy;
    uint2 pixelCoord = uv * numPixels;
    return pixelCoord.y * numPixels.x + pixelCoord.x;
}

uint Random_Seed(float2 uv) {
    return PixelIndex(uv) + _Frame * 240828;
}

float RandomValue(inout uint state) {
    state = state * 747796405 + 2891336453;
    uint result = ((state >> (state >> 28) + 4) ^ state) * 277803737;
    result = (result >> 22) ^ result;
    return result / 4294967295.0;
}

float2 RandomPointInCircle(inout uint state) {
    float angle = 2 * PI * RandomValue(state);
    float2 pointOnCircle = float2(cos(angle), sin(angle));
    return pointOnCircle * sqrt(RandomValue(state));
}

float RandomValueNormalDist(inout uint state) {
    float theta = 2 * PI * RandomValue(state);
    float rho = sqrt(-2 * log(RandomValue(state)));
    return rho * cos(theta);
}

float3 RandomDirection(inout uint state) {
    float x = RandomValueNormalDist(state);
    float y = RandomValueNormalDist(state);
    float z = RandomValueNormalDist(state);
    return normalize(float3(x, y, z));
}

float3 RandomDirectionHemisphere(float3 normal, inout uint state) {
    float3 dir = RandomDirection(state);
    return sign(dot(dir, normal)) * dir;
}

#endif