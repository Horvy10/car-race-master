using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    public float Speed { get; private set; }
    public bool IsRunning { get; set; } = true;

    float baseSpeed;

    void Awake()
    {
        GetComponent<Rigidbody>().isKinematic = true;
    }

    public void Init(float speed)
    {
        baseSpeed = speed;
        Speed = speed;
    }

    void Update()
    {
        if (!IsRunning)
        {
            Speed = 0f;
            return;
        }

        // pomalšie auto pred nami: nepredbehneme ho (premávka sa nesmie prekrývať)
        float target = baseSpeed;
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.forward * 2.2f;
        if (Physics.Raycast(origin, Vector3.forward, out hit, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            TrafficCar ahead = hit.collider.GetComponentInParent<TrafficCar>();
            if (ahead != null) target = Mathf.Min(target, ahead.Speed);
        }

        Speed = Mathf.MoveTowards(Speed, target, 15f * Time.deltaTime);
        transform.position += Vector3.forward * Speed * Time.deltaTime;
    }
}