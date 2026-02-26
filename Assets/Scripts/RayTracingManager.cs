using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;

[ExecuteAlways]
public partial class RayTracingManager : MonoBehaviour
{
    public static RayTracingManager Instance;
    enum DebugMode { Off, Normals, BoxTests, TriTests }

    [SerializeField] bool rayTrace = false;
    [SerializeField] DebugMode debugView = DebugMode.Off;
    [SerializeField, Range(1, 1000)] float debugViewBoxLimit = 70;
    [SerializeField, Range(0, 1000)] float debugViewTriLimit = 100;
    [SerializeField, Range(0, 100)] int maxBounceCount = 1, numRaysPerPixel = 1;
    [SerializeField] float divergeStrength = 0, depthOfFieldStrength = 0;
    [SerializeField] float focusDistance = 0;
    [SerializeField] RayTraceSkyManager skyManager;
    [SerializeField] Material fullScreenMaterial;
    [SerializeField] ComputeShader localToWorldComp;

    List<Sphere> spheresList;
    [NonSerialized] public List<Triangle> trianglesList;
    [NonSerialized] public List<MeshInfo> meshInfoList;
    [NonSerialized] public List<BVHNode> bvhList;

    private ComputeBuffer skyBuffer;
    private ComputeBuffer spheresBuffer;
    private ComputeBuffer trianglesBuffer;
    private ComputeBuffer meshInfoBuffer;
    private ComputeBuffer bvhBuffer;

    private RenderTexture mainTexOld;
    private int numFramesPassed;

    static readonly int
        rayTraceActiveId = Shader.PropertyToID("_RayTraceActive"),
        viewParamsId = Shader.PropertyToID("_ViewParams"),
        camLocalToWorldMatrixId = Shader.PropertyToID("_CamLocalToWorldMatrix"),
        maxBounceCountId = Shader.PropertyToID("_MaxBounceCount"),
        numRaysPerPixelId = Shader.PropertyToID("_NumRaysPerPixel"),
        viewPointJitterId = Shader.PropertyToID("_ViewPointJitterStrength"),
        originJitterId = Shader.PropertyToID("_OriginJitterStrength"),
        frameId = Shader.PropertyToID("_Frame"),
        skyBufferId = Shader.PropertyToID("_SkyDataBuffer"),
        sphereListId = Shader.PropertyToID("_Spheres"),
        numSpheresId = Shader.PropertyToID("_NumSpheres"),
        triangleListId = Shader.PropertyToID("_Triangles"),
        allMeshInfoId = Shader.PropertyToID("_AllMeshInfo"),
        allBvhNodesId = Shader.PropertyToID("_AllBVHNodes"),
        numMeshesId = Shader.PropertyToID("_NumMeshes"),
        mainTexOldId = Shader.PropertyToID("_MainTexOld"),
        numFramesPassedId = Shader.PropertyToID("_NumFramesPassed"),
        debugViewModeId = Shader.PropertyToID("_DebugView"),
        debugViewTriLimitId = Shader.PropertyToID("_DebugViewTriLimit"),
        debugViewBoxLimitId = Shader.PropertyToID("_DebugViewBoxLimit");



    private void OnEnable() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        OnDisable();
        if (Instance == null || Instance == this) {
            Instance = this;
        } else {
            Destroy(gameObject);
        }
        numFramesPassed = 0;
        skyBuffer ??= new(1, 68);
        mainTexOld = mainTexOld != null ? mainTexOld : new RenderTexture(Screen.width, Screen.height, 0);
        RenderPipelineManager.beginCameraRendering += RefreshMaterialProperties;
        RenderPipelineManager.endCameraRendering += SetPreviousFrameTexture;
        RefreshTrackedRenderers(true, true);
    }

    private void Update() {
        RefreshTrackedRenderers(rayTrace, false);
    }

    private void OnDisable() {
        skyBuffer?.Release();
        spheresBuffer?.Release();
        trianglesBuffer?.Release();
        meshInfoBuffer?.Release();
        bvhBuffer?.Release();
        if (mainTexOld != null) mainTexOld.Release();
        RenderPipelineManager.beginCameraRendering -= RefreshMaterialProperties;
        RenderPipelineManager.endCameraRendering -= SetPreviousFrameTexture;
        Instance = null;
    }

    public void RefreshTrackedRenderers(bool rayTrace, bool updateBvh) {
        if (!rayTrace) {
            return;
        }

        RayTraceSphereRenderer[] sphereRnd = FindObjectsByType<RayTraceSphereRenderer>(FindObjectsSortMode.None);
        RayTraceMeshRenderer[] meshRnd = FindObjectsByType<RayTraceMeshRenderer>(FindObjectsSortMode.None);
        spheresList = new();
        meshInfoList = new();
        if (updateBvh) {
            trianglesList = new();
            bvhList = new();
        }

        foreach (RayTraceSphereRenderer rnd in sphereRnd) {
            rnd.UpdateData();
            spheresList.Add(rnd.sphere);
        }

        foreach (RayTraceMeshRenderer rnd in meshRnd) {
            rnd.UpdateData(updateBvh);
            meshInfoList.Add(rnd.meshInfo);
        }

        if (spheresList.Count > 0) {
            spheresBuffer?.Release();
            spheresBuffer = new(spheresList.Count, (int) RayTraceDataSize.Sphere);
            spheresBuffer.SetData(spheresList);
        }

        if (meshInfoList.Count > 0) {
            meshInfoBuffer?.Release();

            meshInfoBuffer = new(meshInfoList.Count, (int) RayTraceDataSize.MeshInfo);
            meshInfoBuffer.SetData(meshInfoList);

            if (updateBvh) {
                trianglesBuffer?.Release();
                bvhBuffer?.Release();
                trianglesBuffer = new(trianglesList.Count, (int) RayTraceDataSize.Triangle);
                trianglesBuffer.SetData(trianglesList);

                bvhBuffer = new(bvhList.Count, (int) RayTraceDataSize.BVHNode);
                bvhBuffer.SetData(bvhList);
            }
        }

        skyBuffer ??= new(1, (int) RayTraceDataSize.RayTraceSkyData);
        skyBuffer.SetData(new RayTraceSkyData[] { skyManager.skyData });
    }

    private void SetPreviousFrameTexture(ScriptableRenderContext context, Camera cam) {
        if (!rayTrace) {
            numFramesPassed = 0;
            return;
        }

        if (cam.CompareTag("MainCamera")) {
            numFramesPassed++;
            if (numFramesPassed > 1) {
                Graphics.Blit(cam.activeTexture, mainTexOld);
            }
        }
    }

    private void RefreshMaterialProperties(ScriptableRenderContext context, Camera cam) {
        fullScreenMaterial.SetInteger(rayTraceActiveId, rayTrace ? 1 : 0);
        if (!rayTrace) {
            return;
        }

        float planeHeight = (cam.nearClipPlane + focusDistance) * Tan(cam.fieldOfView * Deg2Rad * 0.5f) * 2;
        float planeWidth = cam.aspect * planeHeight;
        

        fullScreenMaterial.SetInteger(rayTraceActiveId, rayTrace ? 1 : 0);
        fullScreenMaterial.SetVector(viewParamsId, new Vector3(planeWidth, planeHeight, cam.nearClipPlane + focusDistance));
        fullScreenMaterial.SetMatrix(camLocalToWorldMatrixId, cam.transform.localToWorldMatrix);
        fullScreenMaterial.SetInteger(maxBounceCountId, maxBounceCount);
        fullScreenMaterial.SetInteger(numRaysPerPixelId, numRaysPerPixel);
        fullScreenMaterial.SetFloat(viewPointJitterId, divergeStrength);        
        fullScreenMaterial.SetFloat(originJitterId, depthOfFieldStrength);

        fullScreenMaterial.SetInteger(frameId, Time.frameCount);

        if (spheresList.Count > 0) {
            fullScreenMaterial.SetBuffer(sphereListId, spheresBuffer);
        }
        fullScreenMaterial.SetInteger(numSpheresId, spheresList.Count);

        if (meshInfoList.Count > 0) {
            fullScreenMaterial.SetBuffer(triangleListId, trianglesBuffer);
            fullScreenMaterial.SetBuffer(allMeshInfoId, meshInfoBuffer);
            fullScreenMaterial.SetBuffer(allBvhNodesId, bvhBuffer);
        }
        fullScreenMaterial.SetInteger(numMeshesId, meshInfoList.Count);

        if (cam.CompareTag("MainCamera")) {
            fullScreenMaterial.SetInteger(numFramesPassedId, numFramesPassed);
            fullScreenMaterial.SetTexture(mainTexOldId, mainTexOld);
        } else {
            fullScreenMaterial.SetInteger(numFramesPassedId, 0);
        }

        fullScreenMaterial.SetBuffer(skyBufferId, skyBuffer);

        fullScreenMaterial.SetInt(debugViewModeId, (int) debugView);
        fullScreenMaterial.SetFloat(debugViewTriLimitId, debugViewTriLimit);
        fullScreenMaterial.SetFloat(debugViewBoxLimitId, debugViewBoxLimit);
    }
}
