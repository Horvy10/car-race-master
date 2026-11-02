using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Rýchlosť")]
    public float maxSpeed = 30f;
    public float nitroMultiplier = 1.5f;
    public float acceleration = 12f;

    [Header("Riadenie")]
    public float steerSpeed = 12f;
    public float roadHalfWidth = 4f;

    [Header("Súperi pred nami")]
    public float frontDetectDistance = 2f;

    [Header("Vizuál")]
    public Transform model;
    public float tiltAngle = 15f;

    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; set; } = true;

    void Awake()
    {
        GetComponent<Rigidbody>().isKinematic = true;
    }

    void Update()
    {
        float steerInput = Input.GetAxis("Horizontal");
        bool nitro = Input.GetKey(KeyCode.Space);

        float targetSpeed = IsRunning ? maxSpeed * (nitro ? nitroMultiplier : 1f) : 0f;

        if (IsRunning)
            targetSpeed = Mathf.Min(targetSpeed, FrontSpeedLimit());

        float rate = acceleration;
        if (CurrentSpeed > targetSpeed) rate = acceleration * 3f;
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.deltaTime);

        Vector3 pos = transform.position;
        float blockerSpeed;

        // 1) dopredu: zastaví nás len auto pred nami
        Vector3 tryZ = new Vector3(pos.x, pos.y, pos.z + CurrentSpeed * Time.deltaTime);
        if (CarCollision.Blocked(transform, tryZ, Vector3.forward, out blockerSpeed))
            CurrentSpeed = Mathf.Min(CurrentSpeed, blockerSpeed);
        else
            pos = tryZ;

        // 2) do strany: nepustí nás len auto na tej strane
        if (IsRunning)
        {
            float newX = Mathf.Clamp(pos.x + steerInput * steerSpeed * Time.deltaTime, -roadHalfWidth, roadHalfWidth);
            float dx = newX - pos.x;
            if (Mathf.Abs(dx) > 0.0001f)
            {
                Vector3 tryX = new Vector3(newX, pos.y, pos.z);
                Vector3 dir = dx > 0f ? Vector3.right : Vector3.left;
                if (!CarCollision.Blocked(transform, tryX, dir, out blockerSpeed))
                    pos = tryX;
            }
        }

        transform.position = pos;

        if (model != null)
        {
            Quaternion target = Quaternion.Euler(0f, steerInput * tiltAngle, 0f);
            model.localRotation = Quaternion.Lerp(model.localRotation, target, 10f * Time.deltaTime);
        }
    }

    // mäkké brzdenie pred autom, aby to nevyzeralo ako náraz do steny
    float FrontSpeedLimit()
    {
        Vector3 center = transform.position + Vector3.forward * 1.9f;
        Vector3 half = new Vector3(0.75f, 0.4f, 0.05f);

        RaycastHit hit;
        if (Physics.BoxCast(center, half, Vector3.forward, out hit, Quaternion.identity,
                            frontDetectDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            EnemyCar enemy = hit.collider.GetComponentInParent<EnemyCar>();
            if (enemy != null)
                return Mathf.Max(0f, enemy.CurrentSpeed * 0.9f);
        }
        return float.MaxValue;
    }
}