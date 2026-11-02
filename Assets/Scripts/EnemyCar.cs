using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyCar : MonoBehaviour
{
    [Header("Rýchlosť")]
    public float maxSpeed = 26f;
    public float acceleration = 12f;

    [Header("Pruhy")]
    public float[] lanes = { -3f, -1f, 1f, 3f };
    public float laneChangeSpeed = 6f;
    public Vector2 laneChangeInterval = new Vector2(3f, 7f);

    [Header("Detekcia auta pred sebou")]
    public float lookAhead = 12f;

    [Header("Fér hra (rubber band)")]
    public Transform player;
    public float rubberBand = 0.12f;
    public float rubberBandDelay = 5f;   // koľko sekúnd po štarte nič nedoťahuje
    public float rubberBandRamp = 5f;    // za koľko sekúnd sa naplno zapne
    [Range(0f, 1f)] public float slowDownFactor = 0.25f; // súper vpredu sa spomaľuje len slabo

    [Header("Charakter súpera (každý iný)")]
    public Vector2 speedMultiplierRange = new Vector2(0.98f, 1.02f);
    public Vector2 preferredGapRange = new Vector2(-5f, 20f);

    [Header("Vizuál")]
    public Transform model;
    public float tiltAngle = 15f;

    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; set; } = true;

    float targetX;
    float nextChangeTime;
    float speedMultiplier;
    float preferredGap;
    float runTime;

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
        if (IsRunning) runTime += Time.deltaTime;

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

        // po štarte sa rubber band zapína postupne
        float ramp = Mathf.Clamp01((runTime - rubberBandDelay) / Mathf.Max(0.01f, rubberBandRamp));

        // kladné = súper je za svojou cieľovou polohou, záporné = je pred ňou
        float diff = (player.position.z + preferredGap) - transform.position.z;
        float k = Mathf.Clamp(diff / 40f, -1f, 1f);
        if (k < 0f) k *= slowDownFactor;   // pred tebou sa len mierne spomalí

        return 1f + k * rubberBand * ramp;
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