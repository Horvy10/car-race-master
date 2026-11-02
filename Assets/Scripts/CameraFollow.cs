using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 4f, -8f);
    public float smooth = 6f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        desired.x = Mathf.Lerp(transform.position.x, target.position.x * 0.5f, smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);
        transform.LookAt(target.position + Vector3.forward * 5f);
    }
}