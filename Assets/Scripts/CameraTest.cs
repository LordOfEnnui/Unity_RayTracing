using UnityEngine;
using static UnityEngine.Mathf;

public class CameraTest : MonoBehaviour
{
    [SerializeField]
    Camera cam;

    Transform camT;
    float planeHeight, planeWidth;

    [SerializeField, Min(2)] private int debugPointCountX, debugPointCountY;

    private void OnDrawGizmosSelected() {
        camT = cam.transform;
        planeHeight = cam.nearClipPlane * Tan(cam.fieldOfView * Deg2Rad * 0.5f) * 2;
        planeWidth = cam.aspect * planeHeight;
        Vector3 nearPlaneBottomLeftLocal = new(-planeWidth / 2, -planeHeight / 2, cam.nearClipPlane + 0.06f);

        for (int x = 0; x < debugPointCountX; x++) {
            for (int y = 0; y < debugPointCountY; y++) {
                float tx = x / (debugPointCountX - 1f);
                float ty = y / (debugPointCountY - 1f);

                Vector3 pointLocal = nearPlaneBottomLeftLocal + new Vector3(planeWidth * tx, planeHeight * ty);
                Vector3 point = camT.position + camT.right * pointLocal.x + camT.up * pointLocal.y + camT.forward * pointLocal.z;

                Gizmos.DrawSphere(point, 0.05f);
                Gizmos.DrawRay(camT.position, (point - camT.position).normalized * 4f);
            }
        }
    }
}
