using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Hlavné menu: meno, režim, level, Hrať, obchod, top 10
public class MainMenuUI : MonoBehaviour
{
    // Názvy scén z Build Settings. Keď Lukáš spraví finálnu scénu pretekov, zmeň RaceScene.
    public const string RaceScene = "TestLukas";
    public const string EndlessScene = "Endless";

    bool endlessMode;
    bool topEndless;

    InputField nameInput;
    Text coinsText, shopCoinsText, topText;
    Button raceTab, endlessTab, topRaceTab, topEndlessTab;
    readonly List<Button> levelButtons = new List<Button>();
    readonly List<Button> buyButtons = new List<Button>();
    readonly List<Text> upgradeTexts = new List<Text>();
    GameObject shopPanel, topPanel;

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    void Start()
    {
        Time.timeScale = 1f;
        GameSession.CurrentLevel = Mathf.Clamp(GameSession.CurrentLevel, 1, GameSession.UnlockedLevel);

        Transform canvas = UIKit.Canvas("MenuUI");
        UIKit.Overlay(canvas, "Background", new Color(0.06f, 0.07f, 0.1f, 1f));
        BuildMenu(canvas);
        BuildShop(canvas);
        BuildTop(canvas);
        Refresh();
    }

    void BuildMenu(Transform c)
    {
        Text title = UIKit.Label(c, "Car Race Game", 110, Center, new Vector2(0f, 410f), new Vector2(1400f, 150f));
        title.fontStyle = FontStyle.Bold;
        title.color = UIKit.Accent;

        coinsText = UIKit.Label(c, "", 44, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(500f, 60f), TextAnchor.MiddleRight);

        UIKit.Label(c, "Meno hráča", 36, Center, new Vector2(0f, 290f), new Vector2(600f, 50f));
        nameInput = UIKit.Input(c, FirebaseService.PlayerName, Center, new Vector2(0f, 225f), new Vector2(520f, 75f));

        UIKit.Label(c, "Režim", 36, Center, new Vector2(0f, 140f), new Vector2(600f, 50f));
        raceTab = UIKit.AddButton(c, "Preteky", Center, new Vector2(-170f, 75f), new Vector2(320f, 80f), () => { endlessMode = false; Refresh(); });
        endlessTab = UIKit.AddButton(c, "Nekonečný", Center, new Vector2(170f, 75f), new Vector2(320f, 80f), () => { endlessMode = true; Refresh(); });

        UIKit.Label(c, "Level", 36, Center, new Vector2(0f, -10f), new Vector2(600f, 50f));
        for (int i = 1; i <= GameSession.LevelCount; i++)
        {
            int level = i;
            float x = (i - (GameSession.LevelCount + 1) / 2f) * 130f;
            levelButtons.Add(UIKit.AddButton(c, i.ToString(), Center, new Vector2(x, -80f), new Vector2(110f, 90f),
                () => { GameSession.CurrentLevel = level; Refresh(); }));
        }

        Button play = UIKit.AddButton(c, "HRAŤ", Center, new Vector2(0f, -220f), new Vector2(440f, 110f), Play);
        UIKit.SetColor(play, UIKit.Accent);
        play.GetComponentInChildren<Text>().fontSize = 54;

        UIKit.AddButton(c, "Obchod", Center, new Vector2(-170f, -360f), new Vector2(320f, 80f), () => Open(shopPanel));
        UIKit.AddButton(c, "Top 10", Center, new Vector2(170f, -360f), new Vector2(320f, 80f), () => { Open(topPanel); LoadTop(); });
    }

    void BuildShop(Transform c)
    {
        shopPanel = UIKit.Overlay(c, "Shop", new Color(0.04f, 0.05f, 0.08f, 0.97f));
        Transform p = shopPanel.transform;

        UIKit.Label(p, "OBCHOD", 90, Center, new Vector2(0f, 380f), new Vector2(800f, 120f)).fontStyle = FontStyle.Bold;
        shopCoinsText = UIKit.Label(p, "", 44, Center, new Vector2(0f, 290f), new Vector2(800f, 60f));

        for (int i = 0; i < Shop.Names.Length; i++)
        {
            int index = i;
            float y = 170f - i * 120f;
            UIKit.Label(p, Shop.Names[i], 40, Center, new Vector2(-330f, y), new Vector2(400f, 80f), TextAnchor.MiddleLeft);
            upgradeTexts.Add(UIKit.Label(p, "", 40, Center, new Vector2(30f, y), new Vector2(240f, 80f)));
            buyButtons.Add(UIKit.AddButton(p, "", Center, new Vector2(330f, y), new Vector2(300f, 85f),
                () => { Shop.Buy(index); Refresh(); }));
        }

        UIKit.AddButton(p, "Späť", Center, new Vector2(0f, -380f), new Vector2(320f, 85f), () => shopPanel.SetActive(false));
        shopPanel.SetActive(false);
    }

    void BuildTop(Transform c)
    {
        topPanel = UIKit.Overlay(c, "Top10", new Color(0.04f, 0.05f, 0.08f, 0.97f));
        Transform p = topPanel.transform;

        UIKit.Label(p, "TOP 10", 90, Center, new Vector2(0f, 400f), new Vector2(800f, 120f)).fontStyle = FontStyle.Bold;
        topRaceTab = UIKit.AddButton(p, "Preteky", Center, new Vector2(-170f, 300f), new Vector2(320f, 75f), () => { topEndless = false; LoadTop(); });
        topEndlessTab = UIKit.AddButton(p, "Nekonečný", Center, new Vector2(170f, 300f), new Vector2(320f, 75f), () => { topEndless = true; LoadTop(); });
        topText = UIKit.Label(p, "", 36, Center, new Vector2(0f, -20f), new Vector2(1300f, 520f), TextAnchor.UpperLeft);

        UIKit.AddButton(p, "Späť", Center, new Vector2(0f, -400f), new Vector2(320f, 85f), () => topPanel.SetActive(false));
        topPanel.SetActive(false);
    }

    void Open(GameObject panel)
    {
        Refresh();
        panel.SetActive(true);
    }

    void Refresh()
    {
        coinsText.text = "Mince: " + Shop.Coins;
        shopCoinsText.text = "Mince: " + Shop.Coins;

        UIKit.SetColor(raceTab, endlessMode ? UIKit.ButtonColor : UIKit.Accent);
        UIKit.SetColor(endlessTab, endlessMode ? UIKit.Accent : UIKit.ButtonColor);

        for (int i = 0; i < levelButtons.Count; i++)
        {
            int level = i + 1;
            levelButtons[i].interactable = level <= GameSession.UnlockedLevel;
            UIKit.SetColor(levelButtons[i], level == GameSession.CurrentLevel ? UIKit.Accent : UIKit.ButtonColor);
        }

        for (int i = 0; i < buyButtons.Count; i++)
        {
            bool max = Shop.Level(i) >= Shop.MaxLevel;
            upgradeTexts[i].text = "Stupeň " + Shop.Level(i) + "/" + Shop.MaxLevel;
            UIKit.SetText(buyButtons[i], max ? "MAX" : "Kúpiť (" + Shop.Price(i) + ")");
            buyButtons[i].interactable = !max && Shop.Coins >= Shop.Price(i);
        }
    }

    void LoadTop()
    {
        UIKit.SetColor(topRaceTab, topEndless ? UIKit.ButtonColor : UIKit.Accent);
        UIKit.SetColor(topEndlessTab, topEndless ? UIKit.Accent : UIKit.ButtonColor);
        topText.text = "Načítavam...";

        bool endless = topEndless;
        FirebaseService.LoadTop10(endless ? FirebaseService.Endless : FirebaseService.Race, list =>
        {
            if (this == null || endless != topEndless) return;   // menu je už zatvorené alebo sa prepla záložka
            if (list == null) { topText.text = "Rebríček sa nepodarilo načítať."; return; }
            if (list.Count == 0) { topText.text = "Zatiaľ žiadne výsledky."; return; }

            string s = "";
            for (int i = 0; i < list.Count; i++)
            {
                ScoreEntry e = list[i];
                string score = endless ? e.score + " m" : e.score + ". miesto";
                s += (i + 1) + ".  " + e.name + "   " + score + "   " + e.time.ToString("F1") + " s   Level " + e.level + "   " + e.date + "\n";
            }
            topText.text = s;
        });
    }

    void Play()
    {
        string playerName = nameInput.text.Trim();
        FirebaseService.PlayerName = playerName.Length > 0 ? playerName : "Hráč";
        SceneManager.LoadScene(endlessMode ? EndlessScene : RaceScene);
    }
}
