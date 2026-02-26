using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

public enum RayTraceDataSize {
    RayTraceMaterial = 60,
    Sphere = 16 + RayTraceDataSize.RayTraceMaterial,
    Triangle = 72,
    MeshInfo = 132 + RayTraceDataSize.RayTraceMaterial,
    BVHNode = 40,
    RayTraceSkyData = 68
} 

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RayTracingMaterial {
    public Color color; //Color = float4
    [Range(0.0f, 1.0f)] public float smoothness;
    [Range(0.0f, 1.0f)] public float specularProbability;
    public Color specularColor;
    public Color emissionColor;
    public float emissionStrength;
}

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Sphere {
    public float3 position;
    public float radius;
    public RayTracingMaterial material;
}

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Triangle {
    public float3 posA, posB, posC;
    public float3 normalA, normalB, normalC;
};

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MeshInfo {
    public uint firstNodeIndex;
    public float4x4 localToWorldMatrix;
    public float4x4 worldToLocalMatrix;
    public RayTracingMaterial material;
};

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BVHNode {
    public float3 boundMin, boundMax;
    public uint firstTriangleIndex, numTriangles;
    public int childAIndex, childBIndex;
}