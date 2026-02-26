using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[GenerateHLSL(PackingRules.Exact, false)]
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RayTraceSkyData {
    public Color GroundColor, SkyColorHorizon, SkyColorZenith;
    public Vector3 SunLightDirection;
    public int SunFocus;
    public float SunIntensity;
}

[ExecuteAlways]
public class RayTraceSkyManager : MonoBehaviour {

    public RayTraceSkyData skyData;
    public static RayTraceSkyManager Instance;
    [SerializeField] private Light sun;

    private void Start() {
        Instance = this;

    }

    private void Update() {
#if UNITY_EDITOR
        skyData.SunLightDirection = sun.transform.forward;
        skyData.SunIntensity = sun.intensity;
#endif
    }
}
