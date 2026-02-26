using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.Mathf;

public struct BVHDebugData {
    public struct MinMaxTotal {
        public float min, max, total;

        public MinMaxTotal(float value) {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            total = value;
        }

        public void Update(float value) {
            min = Min(min, value);
            max = Max(max, value);
            total += value;
        }

        public readonly string ToString(float value) {
            return
                $" - Min: {min}\n" +
                $" - Max: {max}\n" +
                $" - Mean: {total / value:f2}\n";
        }
    }

    public float time;
    public int numTriangles, numNodes, numLeaves;
    public MinMaxTotal leafDepth, leafTris;

    public override readonly string ToString() {
        return
            $"Time (ms): {(int) (time * 1000)}\n" +
            $"Triangles: {numTriangles}\n" +
            $"Node Count: {numNodes}\n" +
            $"Leaf Count: {numLeaves}\n" +
            $"Leaf Depth: \n" +
            leafDepth.ToString(numLeaves) +
            $"Leaf Tris: \n" +
            leafTris.ToString(numLeaves);
    }
}

[BurstCompile(CompileSynchronously = true)]
public class RayTraceMeshRenderer : MonoBehaviour {
    [SerializeField] private bool useSpecularColor;
    [SerializeField] private bool useSpecularProbability;
    public RayTracingMaterial material;
    [SerializeField, Range(0, 32)] int boundingVolumeHierarchyMaxDepth = 32;   
    [SerializeField, Range(0, 20)] int visDepth = 10;

    [NonSerialized]
    public MeshInfo meshInfo;

    private uint rootNodeIndex;

    [NonSerialized]
    private BVHDebugData debugData;

    public void UpdateData(bool updateBvh) {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;

        if (updateBvh) {
            debugData = new BVHDebugData {
                time = Time.realtimeSinceStartup,
                numTriangles = 0,
                numNodes = 0,
                numLeaves = 0,
                leafDepth = new BVHDebugData.MinMaxTotal(0),
                leafTris = new BVHDebugData.MinMaxTotal(0)
            };
            GenerateBoundingVolumeHierarchy(mesh.triangles, mesh.vertices, mesh.normals);
            debugData.time = Time.realtimeSinceStartup - debugData.time;
            Debug.Log(debugData);
        }

        RayTracingMaterial mat = material;
        mat.specularProbability = useSpecularProbability ? material.specularProbability : 1;
        mat.specularColor = useSpecularColor ? material.specularColor : material.color;

        meshInfo = new MeshInfo {
            firstNodeIndex = rootNodeIndex,
            localToWorldMatrix = transform.localToWorldMatrix,
            worldToLocalMatrix = transform.worldToLocalMatrix,
            material = mat
        };
    }

    private void OnDrawGizmosSelected() {
        if (RayTracingManager.Instance == null || visDepth == 0) return;

        void DrawBVH(int nodeIndex, int depth = 0) {
            if (depth > Min(boundingVolumeHierarchyMaxDepth, visDepth)) return;
            BVHNode node = RayTracingManager.Instance.bvhList[nodeIndex];

            Vector3 center = (node.boundMin + node.boundMax) * 0.5f;
            Vector3 size = node.boundMax - node.boundMin;
            Color col = Color.HSVToRGB(depth / 6f % 1, 1, 1);
            col.a = 1 / visDepth;
            Gizmos.color = col;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(center, size);
            if (depth == visDepth) {
                col.a = 1;
                Gizmos.color = col;
                Gizmos.DrawCube(center, size);
            }

            if (node.childAIndex >= 0) {
                DrawBVH(node.childAIndex, depth + 1);
            }

            if (node.childBIndex >= 0) {
                DrawBVH(node.childBIndex, depth + 1);
            }
        }

        DrawBVH((int) rootNodeIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile(CompileSynchronously = true)]
    public void GenerateBoundingVolumeHierarchy(int[] tris, Vector3[] vertices, Vector3[] normals) {
        BVHNode root;

        int numTriangles = tris.Length / 3;
        List<Triangle> rootTriangles = new();

        for (int t = 0; t < numTriangles; t++) {
            int triangleIndex = t * 3;
            int a = tris[triangleIndex];
            int b = tris[triangleIndex + 1];
            int c = tris[triangleIndex + 2];
            rootTriangles.Add(new Triangle {
                posA = vertices[a],
                posB = vertices[b],
                posC = vertices[c],
                normalA = normals[a],
                normalB = normals[b],
                normalC = normals[c]
            });
        }

        float3 boundsMin = float.PositiveInfinity, boundsMax = float.NegativeInfinity;
        foreach (float3 vert in vertices) {
            boundsMin = math.min(boundsMin, vert);
            boundsMax = math.max(boundsMax, vert);
        }

        int ChildAIndex, ChildBIndex;
        int firstTriangleIndex = RayTracingManager.Instance.trianglesList.Count;
        BoundingVolumeSplit(rootTriangles, boundsMin, boundsMax, 0, out ChildAIndex, out ChildBIndex);

        root = new() {
            boundMin = boundsMin,
            boundMax = boundsMax,
            firstTriangleIndex = (uint) firstTriangleIndex,
            numTriangles = (uint) numTriangles,
            childAIndex = ChildAIndex,
            childBIndex = ChildBIndex
        };

        rootNodeIndex = (uint) RayTracingManager.Instance.bvhList.Count;
        RayTracingManager.Instance.bvhList.Add(root);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile(CompileSynchronously = true)]
    private void BoundingVolumeSplit(
        List<Triangle> rootTriangles, float3 boundsMin, float3 boundsMax, int depth,
        out int Out_ChildAIndex, out int Out_ChildBIndex
    ) {
        void HandleLeafNode(List<Triangle> rootTriangles, int depth, out int Out_ChildAIndex, out int Out_ChildBIndex) {
            debugData.numTriangles += rootTriangles.Count;
            debugData.numLeaves++;
            debugData.leafDepth.Update(depth);
            debugData.leafTris.Update(rootTriangles.Count);

            RayTracingManager.Instance.trianglesList.AddRange(rootTriangles);

            Out_ChildAIndex = -1;
            Out_ChildBIndex = -1;
        }

        debugData.numNodes++;
        if (depth >= boundingVolumeHierarchyMaxDepth) {
            HandleLeafNode(rootTriangles, depth, out Out_ChildAIndex, out Out_ChildBIndex);
            return;
        }

        BVHNode childA, childB;

        float3 size = boundsMax - boundsMin;
        float3 boundsCenter = (boundsMin + boundsMax) * 0.5f;
        int splitAxis = size.x > Max(size.y, size.z) ? 0 : size.y > size.z ? 1 : 2;

        List<Triangle> A_childTris = new(), B_childTris = new();
        float3
            A_boundsMin = float.PositiveInfinity, 
            B_boundsMin = float.PositiveInfinity,
            A_boundsMax = float.NegativeInfinity, 
            B_boundsMax = float.NegativeInfinity;

        foreach (Triangle tri in rootTriangles) {
            Triangle triI = tri;
            float3 triCenter = (tri.posA + tri.posB + tri.posC) * (1 / 3f);
            if (triCenter[splitAxis] < boundsCenter[splitAxis]) {
                A_childTris.Add(tri);
                ExpandBoundsTriangle(ref A_boundsMin, ref A_boundsMax, ref triI);
            } else {
                B_childTris.Add(tri);
                ExpandBoundsTriangle(ref B_boundsMin, ref B_boundsMax, ref triI);
            }
        }

        if (A_childTris.Count == 0 || B_childTris.Count == 0) {
            HandleLeafNode(rootTriangles, depth, out Out_ChildAIndex, out Out_ChildBIndex);
            return;
        }

        int A_ChildAIndex, A_ChildBIndex;
        int A_firstTriangleIndex = RayTracingManager.Instance.trianglesList.Count;
        BoundingVolumeSplit(A_childTris, A_boundsMin, A_boundsMax, depth + 1, out A_ChildAIndex, out A_ChildBIndex);
        childA = new() {
            boundMin = A_boundsMin,
            boundMax = A_boundsMax,
            firstTriangleIndex = (uint) A_firstTriangleIndex,
            numTriangles = (uint) A_childTris.Count,
            childAIndex = A_ChildAIndex,
            childBIndex = A_ChildBIndex
        };

        int B_ChildAIndex, B_ChildBIndex;
        int B_firstTriangleIndex = RayTracingManager.Instance.trianglesList.Count;
        BoundingVolumeSplit(B_childTris, B_boundsMin, B_boundsMax, depth + 1, out B_ChildAIndex, out B_ChildBIndex);
        childB = new() {
            boundMin = B_boundsMin,
            boundMax = B_boundsMax,
            firstTriangleIndex = (uint) B_firstTriangleIndex,
            numTriangles = (uint) B_childTris.Count,
            childAIndex = B_ChildAIndex,
            childBIndex = B_ChildBIndex
        };

        Out_ChildAIndex = RayTracingManager.Instance.bvhList.Count;
        RayTracingManager.Instance.bvhList.Add(childA);
        Out_ChildBIndex = RayTracingManager.Instance.bvhList.Count;
        RayTracingManager.Instance.bvhList.Add(childB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [BurstCompile(CompileSynchronously = true)]
    private static void ExpandBoundsTriangle(
        ref float3 boundsMin, ref float3 boundsMax, ref Triangle tri
    ) {

        boundsMin = math.min(boundsMin, tri.posA);
        boundsMax = math.max(boundsMax, tri.posA);
        boundsMin = math.min(boundsMin, tri.posB);
        boundsMax = math.max(boundsMax, tri.posB);
        boundsMin = math.min(boundsMin, tri.posC);
        boundsMax = math.max(boundsMax, tri.posC);
    }

    private static float NodeCost(float3 size, int numTriangles) {
        float halfArea = size.x * (size.y + size.z) + size.y * size.z;
        return halfArea * numTriangles;
    }
}