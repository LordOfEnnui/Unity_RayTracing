using UnityEngine;

public class RayTraceSphereRenderer : MonoBehaviour {
    
    public RayTracingMaterial material;
    public Sphere sphere;

    public void UpdateData() {
        sphere = new Sphere {
            position = transform.position,
            radius = transform.localScale.x * 0.5f,
            material = material
        };
    }
}
