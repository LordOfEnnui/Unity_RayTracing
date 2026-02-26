#ifndef RAY_TRACE_HIT
#define RAY_TRACE_HIT
#include "RayTraceData.hlsl"

StructuredBuffer<Sphere> _Spheres;
int _NumSpheres;

StructuredBuffer<Triangle> _Triangles;
StructuredBuffer<MeshInfo> _AllMeshInfo; 
StructuredBuffer<BVHNode> _AllBVHNodes;
int _NumMeshes;
int _MaxBVHDepth = 10;

HitInfo RaySphere(Ray ray, float3 sphereCentre, float sphereRadius)
{
    HitInfo hitInfo = (HitInfo) 0;
    float3 offsetRayOrigin = ray.origin - sphereCentre;
    //See: https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-sphere-intersection.html
    
    float a = dot(ray.dir, ray.dir);
    float b = 2 * dot(offsetRayOrigin, ray.dir);
    float c = dot(offsetRayOrigin, offsetRayOrigin) - sphereRadius * sphereRadius;
    
    float discriminant = b * b - 4 * a * c;
    
    if (discriminant >= 0)
    {
        float dst = (-b - sqrt(discriminant)) / (2 * a);
        
        if (dst >= 0)
        {
            hitInfo.didHit = true;
            hitInfo.dst = dst;
            hitInfo.hitPoint = ray.origin + ray.dir * dst;
            hitInfo.normal = normalize(hitInfo.hitPoint - sphereCentre);
        }
    }
    return hitInfo;
}

//Moller-Trumbore
HitInfo RayTriangle(Ray ray, Triangle tri) {
    float3 edgeAB = tri.posB - tri.posA;
    float3 edgeAC = tri.posC - tri.posA;
    float3 normalVector = cross(edgeAB, edgeAC);
    float3 ao = ray.origin - tri.posA;
    float3 dao = cross(ao, ray.dir);
    
    float determinant = -dot(ray.dir, normalVector);
    float invDet = 1 / determinant;
    
    float dst = dot(ao, normalVector) * invDet;
    float u = dot(edgeAC, dao) * invDet;
    float v = -dot(edgeAB, dao) * invDet;
    float w = 1 - u - v;
    
    HitInfo hitInfo;
    hitInfo.didHit = determinant >= 1E-8 && dst >= 0 && u>=0 && v >= 0 && w >= 0;
    hitInfo.hitPoint = ray.origin + ray.dir * dst;
    hitInfo.normal = normalize(tri.normalA * w + tri.normalB * u + tri.normalC * v);
    hitInfo.dst = dst;
    return hitInfo;
}

//https://tavianator.com/2015/ray_box_nan.html
float RayBoundingBoxDst(Ray ray, float3 boundsMin, float3 boundsMax)
{
    float3 tMin = (boundsMin - ray.origin) * ray.invDir;
    float3 tMax = (boundsMax - ray.origin) * ray.invDir;
    float3 t1 = min(tMin, tMax);
    float3 t2 = max(tMin, tMax);
    float dstFar = min(min(t2.x, t2.y), t2.z);
    float dstNear = max(max(t1.x, t1.y), t1.z);
    
    bool didHit = dstFar >= dstNear && dstFar > 0;
    return didHit ? dstNear > 0 ? dstNear : 0 : 1.#INF; 
}
#endif