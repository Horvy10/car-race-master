using UnityEngine;

// Mince a vylepšenia auta (ukladá sa lokálne cez PlayerPrefs)
public static class Shop
{
    public const int MaxLevel = 3;
    public static readonly string[] Names = { "Max. rýchlosť", "Nitro", "Zrýchlenie", "Zatáčanie" };
    static readonly string[] Keys = { "UpSpeed", "UpNitro", "UpAccel", "UpSteer" };

    public static int Coins
    {
        get { return PlayerPrefs.GetInt("Coins", 0); }
        set { PlayerPrefs.SetInt("Coins", value); PlayerPrefs.Save(); }
    }

    public static int Level(int i) { return PlayerPrefs.GetInt(Keys[i], 0); }

    public static int Price(int i) { return 100 * (Level(i) + 1); }   // 100, 200, 300

    public static bool Buy(int i)
    {
        int price = Price(i);
        if (Level(i) >= MaxLevel || Coins < price) return false;

        PlayerPrefs.SetInt(Keys[i], Level(i) + 1);
        Coins -= price;   // setter uloží aj nový stupeň
        return true;
    }

    // Volá sa pred Start() manažérov, aby EndlessManager zobral vylepšenú rýchlosť ako základ
    public static void Apply(CarController car)
    {
        car.maxSpeed += 2f * Level(0);
        car.nitroMultiplier += 0.1f * Level(1);
        car.acceleration += 2f * Level(2);
        car.steerSpeed += 1.5f * Level(3);
    }

    public static int RaceReward(int position)
    {
        int[] rewards = { 100, 60, 40, 20 };
        return rewards[Mathf.Clamp(position, 1, rewards.Length) - 1];
    }

    public static int EndlessReward(int distance) { return distance / 10; }
}
