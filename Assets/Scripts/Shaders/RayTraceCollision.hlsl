#ifndef RAY_TRACE_COLLISION
#define RAY_TRACE_COLLISION
#include "RayTraceHit.hlsl"

//All in object space
HitInfo RayBVH(Ray ray, uint firstNodeIndex, inout int2 stats) {
    uint nodeStack[32];
    int stackIndex = 0;
    nodeStack[stackIndex++] = firstNodeIndex;
    
    HitInfo result;
    result.dst = 1.#INF;
    
    while (stackIndex > 0) {
        BVHNode node = _AllBVHNodes[nodeStack[--stackIndex]];
        
        if (node.childAIndex < 0) { //leaf node
            stats[1] += node.numTriangles;
            for (uint i_t = 0; i_t < node.numTriangles; i_t++) {
                HitInfo hitInfo = RayTriangle(ray, _Triangles[node.firstTriangleIndex + i_t]);
                if (hitInfo.didHit && hitInfo.dst < result.dst) result = hitInfo;
            }
        } else {
            BVHNode childA = _AllBVHNodes[node.childAIndex];
            BVHNode childB = _AllBVHNodes[node.childBIndex];
                
            float dstA = RayBoundingBoxDst(ray, childA.boundsMin, childA.boundsMax);
            float dstB = RayBoundingBoxDst(ray, childB.boundsMin, childB.boundsMax);
            stats[0] += 2;
                
            bool isNearestA = dstA < dstB;
            float dstNear = isNearestA ? dstA : dstB;
            float dstFar = isNearestA ? dstB : dstA;
            int childIndexNear = isNearestA ? node.childAIndex : node.childBIndex;
            int childIndexFar = isNearestA ? node.childBIndex : node.childAIndex;
                
            if (dstFar < result.dst) nodeStack[stackIndex++] = childIndexFar;
            if (dstNear < result.dst) nodeStack[stackIndex++] = childIndexNear;    
        }
        
    }
    
    return result;
}
 
HitInfo CalculateRayCollision(Ray ray, inout int2 stats) {
    HitInfo closestHit = (HitInfo) 0;
    closestHit.dst = 1.#INF;
    
    for (int i = 0; i < _NumSpheres; i++) {
        Sphere sphere = _Spheres[i];
        HitInfo hitInfo = RaySphere(ray, sphere.position, sphere.radius);
        
        if (hitInfo.didHit && hitInfo.dst < closestHit.dst) {
            closestHit = hitInfo;
            closestHit.material = sphere.material;
        }
    }
    
    for (int i_m = 0; i_m < _NumMeshes; i_m++) {
        MeshInfo meshInfo = _AllMeshInfo[i_m];
        
        Ray rayOS;
        rayOS.origin = mul(meshInfo.worldToLocalMatrix, float4(ray.origin, 1)).xyz;
        rayOS.dir = mul(meshInfo.worldToLocalMatrix, float4(ray.dir, 0)).xyz;
        rayOS.invDir = 1 / rayOS.dir;
        
        BVHNode root = _AllBVHNodes[meshInfo.firstNodeIndex];
        if (RayBoundingBoxDst(rayOS, root.boundsMin, root.boundsMax) > closestHit.dst) continue;
        
        HitInfo hitInfo = RayBVH(rayOS, meshInfo.firstNodeIndex, stats);
        
        if (hitInfo.didHit && hitInfo.dst < closestHit.dst) {
            closestHit.didHit = true;
            closestHit.dst = hitInfo.dst;
            closestHit.hitPoint = ray.origin + ray.dir * hitInfo.dst;
            closestHit.normal = normalize(mul(meshInfo.localToWorldMatrix, float4(hitInfo.normal, 0)).xyz);
            closestHit.material = meshInfo.material;
        }
    }
    
    return closestHit;
}

#endif