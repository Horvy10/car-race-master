using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUD pre preteky aj nekonečný režim: rýchlomer, odpočet, pauza, výhra/prehra.
// Vytvorí sa sám v každej scéne s RaceManager alebo EndlessManager, scény netreba upravovať.
public class GameHUD : MonoBehaviour
{
    public const string MenuScene = "MainMenu";
    const float BarWidth = 320f;

    RaceManager race;
    EndlessManager endless;
    CarController car;

    Text info, speedText, countdown, resultTitle, resultText;
    RectTransform speedFill;
    GameObject pausePanel, resultPanel;
    Button nextButton;
    float startTimer;
    bool paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        // -= najprv: v projekte je vypnutý reload domény, inak by sa HUD pri ďalšom Play vytvoril dvakrát
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // sceneLoaded príde po Awake a pred Start, takže vylepšenia stihneme nastaviť skôr, než si ich EndlessManager prečíta
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;

        RaceManager race = FindAnyObjectByType<RaceManager>();
        EndlessManager endless = FindAnyObjectByType<EndlessManager>();
        if (race == null && endless == null) return;

        new GameObject("GameHUD").AddComponent<GameHUD>().Init(race, endless);
    }

    void Init(RaceManager r, EndlessManager e)
    {
        race = r;
        endless = e;
        car = race != null ? race.player : endless.player;
        Shop.Apply(car);

        if (race != null) race.OnRaceFinished += OnRaceFinished;
        else endless.OnGameOver += OnGameOver;

        Build();
    }

    void OnDestroy()
    {
        if (race != null) race.OnRaceFinished -= OnRaceFinished;
        if (endless != null) endless.OnGameOver -= OnGameOver;
    }

    void Build()
    {
        Transform canvas = UIKit.Canvas("GameUI");
        Vector2 center = new Vector2(0.5f, 0.5f);

        // vľavo hore: level, poradie/čas alebo životy/vzdialenosť
        Image infoBox = UIKit.Box(canvas, UIKit.Panel, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(460f, 170f));
        info = UIKit.Label(infoBox.transform, "", 38, center, Vector2.zero, new Vector2(420f, 150f), TextAnchor.MiddleLeft);

        // vpravo dole: rýchlomer
        Image speedBox = UIKit.Box(canvas, UIKit.Panel, new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(360f, 150f));
        speedText = UIKit.Label(speedBox.transform, "0 km/h", 60, new Vector2(0.5f, 1f), new Vector2(0f, -15f), new Vector2(340f, 80f));
        speedText.fontStyle = FontStyle.Bold;
        UIKit.Box(speedBox.transform, new Color(1f, 1f, 1f, 0.15f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(BarWidth, 22f));
        speedFill = UIKit.Box(speedBox.transform, UIKit.Accent, new Vector2(0f, 0f), new Vector2(20f, 25f), new Vector2(0f, 22f)).rectTransform;

        // stred: odpočet 3-2-1
        countdown = UIKit.Label(canvas, "", 220, center, new Vector2(0f, 120f), new Vector2(1000f, 300f));
        countdown.fontStyle = FontStyle.Bold;
        countdown.color = UIKit.Accent;

        UIKit.AddButton(canvas, "II", new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(100f, 100f), TogglePause);

        pausePanel = UIKit.Overlay(canvas, "Pause", new Color(0f, 0f, 0f, 0.7f));
        UIKit.Label(pausePanel.transform, "PAUZA", 110, center, new Vector2(0f, 220f), new Vector2(800f, 150f)).fontStyle = FontStyle.Bold;
        UIKit.AddButton(pausePanel.transform, "Pokračovať", center, new Vector2(0f, 40f), new Vector2(420f, 100f), TogglePause);
        UIKit.AddButton(pausePanel.transform, "Hrať znova", center, new Vector2(0f, -90f), new Vector2(420f, 100f), Restart);
        UIKit.AddButton(pausePanel.transform, "Menu", center, new Vector2(0f, -220f), new Vector2(420f, 100f), ToMenu);
        pausePanel.SetActive(false);

        resultPanel = UIKit.Overlay(canvas, "Result", new Color(0f, 0f, 0f, 0.75f));
        resultTitle = UIKit.Label(resultPanel.transform, "", 120, center, new Vector2(0f, 280f), new Vector2(1000f, 160f));
        resultTitle.fontStyle = FontStyle.Bold;
        resultText = UIKit.Label(resultPanel.transform, "", 48, center, new Vector2(0f, 80f), new Vector2(1000f, 220f));
        UIKit.AddButton(resultPanel.transform, "Hrať znova", center, new Vector2(-460f, -180f), new Vector2(420f, 100f), Restart);
        nextButton = UIKit.AddButton(resultPanel.transform, "Ďalší level", center, new Vector2(0f, -180f), new Vector2(420f, 100f), NextLevel);
        UIKit.AddButton(resultPanel.transform, "Menu", center, new Vector2(460f, -180f), new Vector2(420f, 100f), ToMenu);
        resultPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();

        speedText.text = Mathf.RoundToInt(car.CurrentSpeed * 3.6f) + " km/h";
        float top = car.maxSpeed * car.nitroMultiplier;
        speedFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(car.CurrentSpeed / top), speedFill.sizeDelta.y);

        bool counting;
        float remaining;
        if (race != null)
        {
            counting = race.State == RaceManager.RaceState.Countdown;
            remaining = race.CountdownRemaining;
            info.text = "Level " + race.Level + "\nPoradie: " + race.PlayerPosition + "/" + race.TotalRacers
                      + "\nČas: " + race.RaceTime.ToString("F1") + " s";
        }
        else
        {
            counting = endless.State == EndlessManager.EndlessState.Countdown;
            remaining = endless.CountdownRemaining;
            info.text = "Level " + endless.Level + "\nŽivoty: " + endless.Lives + "\nVzdialenosť: " + endless.Distance + " m";
            // po náraze bliká na červeno, kým je auto nezraniteľné
            info.color = endless.IsInvulnerable && Mathf.Repeat(Time.time * 6f, 1f) > 0.5f ? Color.red : Color.white;
        }

        if (counting)
        {
            countdown.text = Mathf.CeilToInt(remaining).ToString();
            startTimer = 1f;
        }
        else if (startTimer > 0f)
        {
            startTimer -= Time.deltaTime;
            countdown.text = startTimer > 0f ? "ŠTART!" : "";
        }
    }

    void OnRaceFinished(bool won, int position, float time)
    {
        int coins = Shop.RaceReward(position);
        Shop.Coins += coins;
        FirebaseService.SaveScore(FirebaseService.Race, position, time, race.Level);

        bool hasNext = won && GameSession.CurrentLevel < GameSession.LevelCount;
        ShowResult(won ? "VÝHRA!" : "PREHRA",
                   position + ". miesto z " + race.TotalRacers + "\nČas: " + time.ToString("F1") + " s\n+" + coins + " mincí",
                   hasNext);
    }

    void OnGameOver(int distance, float time)
    {
        int coins = Shop.EndlessReward(distance);
        Shop.Coins += coins;
        FirebaseService.SaveScore(FirebaseService.Endless, distance, time, endless.Level);

        ShowResult("KONIEC HRY", "Vzdialenosť: " + distance + " m\nČas: " + time.ToString("F1") + " s\n+" + coins + " mincí", false);
    }

    void ShowResult(string title, string text, bool hasNext)
    {
        resultTitle.text = title;
        resultTitle.color = title == "PREHRA" ? new Color(1f, 0.3f, 0.3f) : UIKit.Accent;
        resultText.text = text;
        nextButton.interactable = hasNext;
        countdown.text = "";
        resultPanel.SetActive(true);
    }

    void TogglePause()
    {
        if (resultPanel.activeSelf) return;
        paused = !paused;
        Time.timeScale = paused ? 0f : 1f;
        pausePanel.SetActive(paused);
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void NextLevel()
    {
        GameSession.CurrentLevel++;
        Restart();
    }

    void ToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuScene);
    }
}
