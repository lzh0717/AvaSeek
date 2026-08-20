using UnityEngine;

namespace IVAAvatar
{

// Attach to the Main Camera (or any camera) to make it frame the IVARenderer
// as a flat, front-facing 2D view with no perspective distortion.
[ExecuteAlways]
public class IVACameraSetup : MonoBehaviour
{
    public Transform target;      // the IVARenderer GameObject
    public float distance = 5f;
    public float orthographicSize = 1f;

    Camera cam;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        Apply();
    }

    void Update()
    {
        if (cam == null) cam = GetComponent<Camera>();
        Apply();
    }

    void Apply()
    {
        if (cam == null) return;

        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;

        Vector3 targetPos = target != null ? target.position : Vector3.zero;
        transform.position = targetPos + Vector3.back * distance;
        transform.rotation = Quaternion.identity; // looking straight down +Z, no tilt/perspective
    }
}
}
