using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyCar : MonoBehaviour
{
    [Header("Rýchlosť")]
    public float maxSpeed = 26f;
    public float acceleration = 12f;

    [Header("Pruhy")]
    public float[] lanes = { -2.5f, 0f, 2.5f };
    public float laneChangeSpeed = 6f;
    public Vector2 laneChangeInterval = new Vector2(3f, 7f);

    [Header("Detekcia auta pred sebou")]
    public float lookAhead = 12f;

    [Header("Fér hra (rubber band)")]
    public Transform player;
    public float rubberBand = 0.25f;

    [Header("Charakter súpera (každý iný)")]
    public Vector2 speedMultiplierRange = new Vector2(0.9f, 1.1f);
    public Vector2 preferredGapRange = new Vector2(-30f, 30f);

    [Header("Vizuál")]
    public Transform model;
    public float tiltAngle = 15f;

    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; set; } = true;

    float targetX;
    float nextChangeTime;
    float speedMultiplier;
    float preferredGap;

    void Awake()
    {
        GetComponent<Rigidbody>().isKinematic = true;
    }

    void Start()
    {
        speedMultiplier = Random.Range(speedMultiplierRange.x, speedMultiplierRange.y);
        preferredGap = Random.Range(preferredGapRange.x, preferredGapRange.y);

        targetX = NearestLane(transform.position.x);
        ScheduleChange();
    }

    void Update()
    {
        float targetSpeed = 0f;

        if (IsRunning)
        {
            targetSpeed = maxSpeed * speedMultiplier * RubberBandFactor();
            targetSpeed = Mathf.Min(targetSpeed, maxSpeed * 1.1f);

            if (LaneBlocked(targetX))
            {
                targetSpeed *= 0.6f;
                TryChangeLane();
            }
            else if (Time.time >= nextChangeTime)
            {
                TryChangeLane();
                ScheduleChange();
            }
        }

        float rate = IsRunning ? acceleration : acceleration * 2f;
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.z += CurrentSpeed * Time.deltaTime;
        pos.x = Mathf.MoveTowards(pos.x, targetX, laneChangeSpeed * Time.deltaTime);
        transform.position = pos;

        if (model != null)
        {
            float diff = targetX - pos.x;
            float dir = Mathf.Abs(diff) > 0.05f ? Mathf.Sign(diff) : 0f;
            Quaternion t = Quaternion.Euler(0f, dir * tiltAngle, 0f);
            model.localRotation = Quaternion.Lerp(model.localRotation, t, 10f * Time.deltaTime);
        }
    }

    float RubberBandFactor()
    {
        if (player == null) return 1f;
        // kladné = súper je pod svojou cieľovou polohou a zrýchli
        float diff = (player.position.z + preferredGap) - transform.position.z;
        return 1f + Mathf.Clamp(diff / 40f, -1f, 1f) * rubberBand;
    }

    bool LaneBlocked(float x)
    {
        Vector3 origin = new Vector3(x, transform.position.y, transform.position.z + 2.2f);
        return Physics.Raycast(origin, Vector3.forward, lookAhead, ~0, QueryTriggerInteraction.Ignore);
    }

    void TryChangeLane()
    {
        List<float> free = new List<float>();
        foreach (float l in lanes)
            if (!Mathf.Approximately(l, targetX) && !LaneBlocked(l))
                free.Add(l);

        if (free.Count > 0)
            targetX = free[Random.Range(0, free.Count)];
    }

    float NearestLane(float x)
    {
        float best = lanes[0];
        foreach (float l in lanes)
            if (Mathf.Abs(l - x) < Mathf.Abs(best - x)) best = l;
        return best;
    }

    void ScheduleChange()
    {
        nextChangeTime = Time.time + Random.Range(laneChangeInterval.x, laneChangeInterval.y);
    }
}