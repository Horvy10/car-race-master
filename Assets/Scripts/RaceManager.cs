using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RaceManager : MonoBehaviour
{
    public enum RaceState { Countdown, Racing, Finished }

    [Header("Odkazy")]
    public CarController player;
    public List<EnemyCar> enemies = new List<EnemyCar>();
    public Transform finishLine;          // cieľová čiara: nastaví sa sama podľa levelu

    [Header("Preteky")]
    public float finishZ = 600f;          // prepíše sa podľa levelu
    public float countdownTime = 3f;

    [Header("Testovanie")]
    public int debugLevel = 0;            // 0 = vypnuté, 1 až 5 = vynúti level bez menu

    // Hodnoty pre UI
    public RaceState State { get; private set; } = RaceState.Countdown;
    public float CountdownRemaining { get; private set; }
    public float RaceTime { get; private set; }
    public int PlayerPosition { get; private set; } = 1;
    public int TotalRacers { get { return enemies.Count + 1; } }
    public bool PlayerWon { get; private set; }
    public int FinalPosition { get; private set; }
    public int Level { get { return GameSession.CurrentLevel; } }

    // Udalosť pre UI a Firebase: vyhral?, poradie, čas
    public event Action<bool, int, float> OnRaceFinished;

    readonly HashSet<EnemyCar> crossed = new HashSet<EnemyCar>();

    void Start()
    {
        if (debugLevel > 0) GameSession.CurrentLevel = debugLevel;
        ApplyLevel();

        CountdownRemaining = countdownTime;
        SetRunning(false);
    }

    void ApplyLevel()
    {
        LevelData level = GameSession.Current;
        finishZ = level.raceLength;

        for (int i = 0; i < enemies.Count; i++)
        {
            // každý súper je o niečo iný: -1, 0, +1 od základnej rýchlosti
            enemies[i].maxSpeed = level.enemySpeed + (i - 1) * 1f;
            enemies[i].rubberBand = level.enemyRubberBand;
        }

        if (finishLine != null)
        {
            Vector3 p = finishLine.position;
            p.z = finishZ;
            finishLine.position = p;
        }
    }

    void Update()
    {
        switch (State)
        {
            case RaceState.Countdown:
                CountdownRemaining -= Time.deltaTime;
                if (CountdownRemaining <= 0f)
                {
                    State = RaceState.Racing;
                    SetRunning(true);
                }
                break;

            case RaceState.Racing:
                RaceTime += Time.deltaTime;
                UpdateRacing();
                break;

            case RaceState.Finished:
                if (Input.GetKeyDown(KeyCode.R))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

                if (Input.GetKeyDown(KeyCode.N) && PlayerWon && GameSession.CurrentLevel < GameSession.LevelCount)
                {
                    GameSession.CurrentLevel++;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
                break;
        }
    }

    void UpdateRacing()
    {
        float playerZ = player.transform.position.z;

        int ahead = 0;
        foreach (EnemyCar e in enemies)
        {
            float z = e.transform.position.z;
            if (z > playerZ) ahead++;
            if (z >= finishZ) crossed.Add(e);
        }
        PlayerPosition = ahead + 1;

        if (playerZ >= finishZ)
            FinishRace();
    }

    void FinishRace()
    {
        State = RaceState.Finished;
        FinalPosition = crossed.Count + 1;
        PlayerWon = FinalPosition == 1;
        SetRunning(false);

        if (PlayerWon) GameSession.UnlockNext();

        if (OnRaceFinished != null)
            OnRaceFinished(PlayerWon, FinalPosition, RaceTime);
    }

    void SetRunning(bool value)
    {
        player.IsRunning = value;
        foreach (EnemyCar e in enemies)
            e.IsRunning = value;
    }

    // Dočasné zobrazenie, kým nebude hotové UI
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;

        if (State == RaceState.Countdown)
            GUI.Label(new Rect(Screen.width / 2f - 20, 100, 400, 50), Mathf.CeilToInt(CountdownRemaining).ToString(), style);
        else if (State == RaceState.Racing)
            GUI.Label(new Rect(20, 20, 800, 50), "Level " + Level + "   Poradie: " + PlayerPosition + "/" + TotalRacers + "   Čas: " + RaceTime.ToString("F1"), style);
        else
        {
            string next = (PlayerWon && GameSession.CurrentLevel < GameSession.LevelCount) ? "  N = ďalší level" : "";
            GUI.Label(new Rect(20, 20, 1200, 100),
                (PlayerWon ? "VYHRAL SI!" : "PREHRAL SI") + "  " + FinalPosition + ". miesto, čas " + RaceTime.ToString("F1") + "  (R = znova" + next + ")", style);
        }
    }
}