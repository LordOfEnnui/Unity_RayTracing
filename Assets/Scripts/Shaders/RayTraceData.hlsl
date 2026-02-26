#ifndef RAY_TRACE_DATA
#define RAY_TRACE_DATA

struct Ray {
    float3 origin;
    float3 dir;
    float3 invDir;
};

struct RayTracingMaterial {
    float4 color;
    float smoothness;
    float specularProbability;
    float4 specularColor;
    float4 emissionColor;
    float emissionStrength;
};

struct HitInfo {
    bool didHit;
    float dst;
    float3 hitPoint;
    float3 normal;
    RayTracingMaterial material;
};

struct Sphere {
    float3 position;
    float radius;
    RayTracingMaterial material;
};

struct Triangle {
    float3 posA, posB, posC;
    float3 normalA, normalB, normalC;
};

struct MeshInfo {
    uint firstNodeIndex;
    float4x4 localToWorldMatrix;
    float4x4 worldToLocalMatrix;
    RayTracingMaterial material;
};

struct BVHNode {
    float3 boundsMin, boundsMax;
    uint firstTriangleIndex, numTriangles;
    int childAIndex, childBIndex;
};

#endif