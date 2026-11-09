using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndlessManager : MonoBehaviour
{
    public enum EndlessState { Countdown, Playing, GameOver }

    [Header("Odkazy")]
    public CarController player;
    public TrafficCar trafficPrefab;

    [Header("Hra")]
    public float countdownTime = 3f;
    public int startLives = 3;
    public float invulnerableTime = 2f;
    public float crashSpeedFactor = 0.5f;

    [Header("Premávka")]
    public float[] lanes = { -3f, -1f, 1f, 3f };
    public float spawnDistance = 90f;
    public float despawnBehind = 30f;
    public float difficultyRampTime = 90f;   // sekundy do plnej obtiažnosti

    [Header("Rýchlosť hráča")]
    public float speedRampPerSecond = 0.2f;

    [Header("Testovanie")]
    public int debugLevel = 0;               // 0 = vypnuté, 1 až 5 = vynúti level bez menu

    // Hodnoty pre UI
    public EndlessState State { get; private set; } = EndlessState.Countdown;
    public float CountdownRemaining { get; private set; }
    public int Lives { get; private set; }
    public int Distance { get; private set; }
    public float PlayTime { get; private set; }
    public bool IsInvulnerable { get { return invulnerableTimer > 0f; } }
    public int Level { get { return GameSession.CurrentLevel; } }

    // Udalosť pre UI a Firebase: vzdialenosť (skóre), čas
    public event Action<int, float> OnGameOver;

    // hodnoty z levelu
    Vector2 trafficSpeedRange;
    float spawnIntervalEasy;
    float spawnIntervalHard;
    float baseMaxSpeed;
    float maxSpeedCap;

    readonly List<TrafficCar> traffic = new List<TrafficCar>();
    Renderer[] playerRenderers;
    float startZ;
    float spawnTimer;
    float invulnerableTimer;

    void Start()
    {
        if (debugLevel > 0) GameSession.CurrentLevel = debugLevel;
        ApplyLevel();

        startZ = player.transform.position.z;
        Lives = startLives;
        CountdownRemaining = countdownTime;

        if (player.model != null)
            playerRenderers = player.model.GetComponentsInChildren<Renderer>();

        // zmaž premávku, ktorá by mohla zostať v scéne
        foreach (TrafficCar t in FindObjectsByType<TrafficCar>(FindObjectsSortMode.None))
            Destroy(t.gameObject);

        SetRunning(false);
    }

    void ApplyLevel()
    {
        LevelData level = GameSession.Current;
        trafficSpeedRange = new Vector2(level.trafficSpeedMin, level.trafficSpeedMax);
        spawnIntervalEasy = level.spawnIntervalEasy;
        spawnIntervalHard = level.spawnIntervalHard;

        // základ berieme z auta (zahŕňa vylepšenia z obchodu)
        baseMaxSpeed = player.maxSpeed;
        maxSpeedCap = baseMaxSpeed * level.speedCapFactor;
    }

    void Update()
    {
        switch (State)
        {
            case EndlessState.Countdown:
                CountdownRemaining -= Time.deltaTime;
                if (CountdownRemaining <= 0f)
                {
                    State = EndlessState.Playing;
                    SetRunning(true);
                }
                break;

            case EndlessState.Playing:
                UpdatePlaying();
                break;

            case EndlessState.GameOver:
                if (Input.GetKeyDown(KeyCode.R))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;
        }
    }

    void UpdatePlaying()
    {
        PlayTime += Time.deltaTime;
        float difficulty = Mathf.Clamp01(PlayTime / difficultyRampTime);

        // hra sa postupne sťažuje
        player.maxSpeed = Mathf.Min(baseMaxSpeed + speedRampPerSecond * PlayTime, maxSpeedCap);
        Distance = Mathf.Max(0, Mathf.FloorToInt(player.transform.position.z - startZ));

        // spawn premávky
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnWave(difficulty);
            spawnTimer = Mathf.Lerp(spawnIntervalEasy, spawnIntervalHard, difficulty);
        }

        // nezraniteľnosť po nárazi (bliká)
        if (invulnerableTimer > 0f)
        {
            invulnerableTimer -= Time.deltaTime;
            SetPlayerVisible(((int)(Time.time * 10f)) % 2 == 0);
            if (invulnerableTimer <= 0f) SetPlayerVisible(true);
        }
        else
        {
            CheckHit();
        }

        Cleanup();
    }

    void SpawnWave(float difficulty)
    {
        int count = UnityEngine.Random.value < 0.3f + 0.4f * difficulty ? 2 : 1;
        List<float> available = new List<float>(lanes);
        float spawnZ = player.transform.position.z + spawnDistance;

        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int index = UnityEngine.Random.Range(0, available.Count);
            float lane = available[index];
            available.RemoveAt(index);

            if (LaneOccupiedNear(lane, spawnZ)) continue;

            TrafficCar car = Instantiate(trafficPrefab, new Vector3(lane, 0.5f, spawnZ), Quaternion.identity);
            car.Init(UnityEngine.Random.Range(trafficSpeedRange.x, trafficSpeedRange.y));
            traffic.Add(car);
        }
    }

    bool LaneOccupiedNear(float lane, float z)
    {
        foreach (TrafficCar t in traffic)
        {
            if (t == null) continue;
            if (Mathf.Abs(t.transform.position.x - lane) < 0.5f &&
                Mathf.Abs(t.transform.position.z - z) < 14f)
                return true;
        }
        return false;
    }

    void CheckHit()
    {
        Physics.SyncTransforms();
        Collider[] hits = Physics.OverlapBox(player.transform.position, CarCollision.CarHalfExtents,
                                             Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
        {
            if (c.GetComponentInParent<TrafficCar>() != null)
            {
                Crash();
                return;
            }
        }
    }

    void Crash()
    {
        Lives--;
        player.ApplyCrash(crashSpeedFactor);

        if (Lives <= 0)
        {
            EndGame();
            return;
        }
        invulnerableTimer = invulnerableTime;
    }

    void EndGame()
    {
        State = EndlessState.GameOver;
        SetPlayerVisible(true);
        SetRunning(false);

        if (OnGameOver != null)
            OnGameOver(Distance, PlayTime);
    }

    void Cleanup()
    {
        float limit = player.transform.position.z - despawnBehind;
        for (int i = traffic.Count - 1; i >= 0; i--)
        {
            if (traffic[i] == null)
            {
                traffic.RemoveAt(i);
            }
            else if (traffic[i].transform.position.z < limit)
            {
                Destroy(traffic[i].gameObject);
                traffic.RemoveAt(i);
            }
        }
    }

    void SetRunning(bool value)
    {
        player.IsRunning = value;
        foreach (TrafficCar t in traffic)
            if (t != null) t.IsRunning = value;
    }

    void SetPlayerVisible(bool visible)
    {
        if (playerRenderers == null) return;
        foreach (Renderer r in playerRenderers)
            r.enabled = visible;
    }

    // Dočasné zobrazenie, kým nebude hotové UI
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;

        if (State == EndlessState.Countdown)
            GUI.Label(new Rect(Screen.width / 2f - 20, 100, 400, 50), Mathf.CeilToInt(CountdownRemaining).ToString(), style);
        else if (State == EndlessState.Playing)
            GUI.Label(new Rect(20, 20, 900, 50), "Level " + Level + "   Životy: " + Lives + "   Vzdialenosť: " + Distance + " m", style);
        else
            GUI.Label(new Rect(20, 20, 1000, 100), "KONIEC  " + Distance + " m, čas " + PlayTime.ToString("F1") + " s  (R = znova)", style);
    }
}