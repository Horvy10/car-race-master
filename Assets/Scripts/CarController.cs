using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Rýchlosť")]
    public float maxSpeed = 30f;
    public float nitroMultiplier = 1.5f;
    public float acceleration = 15f;

    [Header("Riadenie")]
    public float steerSpeed = 12f;
    public float roadHalfWidth = 4f;

    [Header("Vizuál")]
    public Transform model;
    public float tiltAngle = 15f;

    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; set; } = true;

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    void Update()
    {
        float steerInput = Input.GetAxis("Horizontal");
        bool nitro = Input.GetKey(KeyCode.Space);

        float targetSpeed = IsRunning ? maxSpeed * (nitro ? nitroMultiplier : 1f) : 0f;
        float rate = IsRunning ? acceleration : acceleration * 2f;
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.z += CurrentSpeed * Time.deltaTime;
        if (IsRunning)
            pos.x = Mathf.Clamp(pos.x + steerInput * steerSpeed * Time.deltaTime, -roadHalfWidth, roadHalfWidth);
        transform.position = pos;

        if (model != null)
        {
            Quaternion target = Quaternion.Euler(0f, steerInput * tiltAngle, 0f);
            model.localRotation = Quaternion.Lerp(model.localRotation, target, 10f * Time.deltaTime);
        }
    }
}