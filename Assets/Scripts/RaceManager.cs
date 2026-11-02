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

    [Header("Preteky")]
    public float finishZ = 600f;
    public float countdownTime = 3f;

    // Hodnoty pre UI
    public RaceState State { get; private set; } = RaceState.Countdown;
    public float CountdownRemaining { get; private set; }
    public float RaceTime { get; private set; }
    public int PlayerPosition { get; private set; } = 1;
    public int TotalRacers { get { return enemies.Count + 1; } }
    public bool PlayerWon { get; private set; }
    public int FinalPosition { get; private set; }

    // Udalosť pre UI a Firebase: vyhral?, poradie, čas
    public event Action<bool, int, float> OnRaceFinished;

    readonly HashSet<EnemyCar> crossed = new HashSet<EnemyCar>();

    void Start()
    {
        CountdownRemaining = countdownTime;
        SetRunning(false);
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
            GUI.Label(new Rect(Screen.width / 2f - 20, 100, 200, 50), Mathf.CeilToInt(CountdownRemaining).ToString(), style);
        else if (State == RaceState.Racing)
            GUI.Label(new Rect(20, 20, 500, 50), "Poradie: " + PlayerPosition + "/" + TotalRacers + "   Čas: " + RaceTime.ToString("F1"), style);
        else
            GUI.Label(new Rect(20, 20, 800, 100),
                (PlayerWon ? "VYHRAL SI!" : "PREHRAL SI") + "  " + FinalPosition + ". miesto, čas " + RaceTime.ToString("F1") + "  (R = znova)", style);
    }
}