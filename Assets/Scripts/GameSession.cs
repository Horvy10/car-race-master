using UnityEngine;

public struct LevelData
{
    public string name;
    public float raceLength;          // dĺžka pretekov (cieľ na tejto osi Z)
    public float enemySpeed;          // základná maxSpeed súperov
    public float enemyRubberBand;     // ako silno sa súperi doťahujú
    public float trafficSpeedMin;     // premávka v nekonečnom režime
    public float trafficSpeedMax;
    public float spawnIntervalEasy;   // sekundy medzi vlnami na začiatku
    public float spawnIntervalHard;   // sekundy medzi vlnami po rozbehnutí
    public float speedCapFactor;      // strop rýchlosti hráča = základ × toto

    public LevelData(string name, float raceLength, float enemySpeed, float enemyRubberBand,
                     float trafficSpeedMin, float trafficSpeedMax,
                     float spawnIntervalEasy, float spawnIntervalHard, float speedCapFactor)
    {
        this.name = name;
        this.raceLength = raceLength;
        this.enemySpeed = enemySpeed;
        this.enemyRubberBand = enemyRubberBand;
        this.trafficSpeedMin = trafficSpeedMin;
        this.trafficSpeedMax = trafficSpeedMax;
        this.spawnIntervalEasy = spawnIntervalEasy;
        this.spawnIntervalHard = spawnIntervalHard;
        this.speedCapFactor = speedCapFactor;
    }
}

public static class GameSession
{
    // Tabuľka levelov: tu sa ladí obtiažnosť
    public static readonly LevelData[] Levels =
    {
        new LevelData("Level 1",  500f, 26f, 0.08f, 10f, 18f, 2.0f, 1.2f, 1.30f),
        new LevelData("Level 2",  700f, 28f, 0.10f, 12f, 20f, 1.8f, 1.0f, 1.40f),
        new LevelData("Level 3",  900f, 29f, 0.12f, 12f, 22f, 1.6f, 0.8f, 1.50f),
        new LevelData("Level 4", 1100f, 30f, 0.14f, 14f, 24f, 1.4f, 0.7f, 1.55f),
        new LevelData("Level 5", 1300f, 31f, 0.16f, 15f, 25f, 1.2f, 0.6f, 1.60f),
    };

    // Vybraný level (1 až LevelCount). Marco ho nastaví z menu pred načítaním scény.
    public static int CurrentLevel = 1;

    public static int LevelCount { get { return Levels.Length; } }

    public static LevelData Current
    {
        get { return Levels[Mathf.Clamp(CurrentLevel, 1, Levels.Length) - 1]; }
    }

    // Odomknuté levely (zatiaľ cez PlayerPrefs, Marco ich môže synchronizovať s Firebase)
    public static int UnlockedLevel
    {
        get { return PlayerPrefs.GetInt("UnlockedLevel", 1); }
        set
        {
            PlayerPrefs.SetInt("UnlockedLevel", Mathf.Clamp(value, 1, Levels.Length));
            PlayerPrefs.Save();
        }
    }

    // Zavolá sa po výhre v pretekoch: odomkne ďalší level
    public static void UnlockNext()
    {
        if (CurrentLevel >= UnlockedLevel)
            UnlockedLevel = CurrentLevel + 1;
    }
}