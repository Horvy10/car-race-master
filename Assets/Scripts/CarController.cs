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

    Rigidbody rb;
    float steerInput;
    bool nitro;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        steerInput = Input.GetAxis("Horizontal");
        nitro = Input.GetKey(KeyCode.Space);

        if (model != null)
        {
            Quaternion target = Quaternion.Euler(0f, steerInput * tiltAngle, 0f);
            model.localRotation = Quaternion.Lerp(model.localRotation, target, 10f * Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        if (!IsRunning)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, acceleration * 2f * Time.fixedDeltaTime);
            return;
        }

        float targetSpeed = maxSpeed * (nitro ? nitroMultiplier : 1f);
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        Vector3 pos = rb.position;
        pos.z += CurrentSpeed * Time.fixedDeltaTime;
        pos.x = Mathf.Clamp(pos.x + steerInput * steerSpeed * Time.fixedDeltaTime, -roadHalfWidth, roadHalfWidth);
        rb.MovePosition(pos);
    }
}