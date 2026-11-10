using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

public class ScoreEntry
{
    public string name;
    public int score;      // preteky: poradie (menej = lepšie), nekonečný: vzdialenosť (viac = lepšie)
    public float time;
    public int level;
    public string date;
}

// Ukladanie skóre do Firebase Realtime Database: scores/race/... a scores/endless/...
public static class FirebaseService
{
    public const string Race = "race";
    public const string Endless = "endless";

    static Task<DependencyStatus> init;

    // V projekte je vypnutý reload domény, statické polia treba pri každom Play vynulovať
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { init = null; }

    public static string PlayerName
    {
        get { return PlayerPrefs.GetString("PlayerName", "Hráč"); }
        set { PlayerPrefs.SetString("PlayerName", value); PlayerPrefs.Save(); }
    }

    static void WhenReady(Action action, Action fail)
    {
        if (init == null) init = FirebaseApp.CheckAndFixDependenciesAsync();

        init.ContinueWithOnMainThread(t =>
        {
            if (!t.IsFaulted && t.Result == DependencyStatus.Available)
                action();
            else
            {
                Debug.LogError("Firebase nie je dostupné: " + (t.IsFaulted ? t.Exception.GetBaseException().Message : t.Result.ToString()));
                fail();
            }
        });
    }

    static DatabaseReference Scores(string mode)
    {
        return FirebaseDatabase.DefaultInstance.GetReference("scores").Child(mode);
    }

    public static void SaveScore(string mode, int score, float time, int level)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "name", PlayerName },
            { "score", score },
            { "time", Math.Round(time, 2) },
            { "level", level },
            { "date", DateTime.Now.ToString("yyyy-MM-dd HH:mm") },
        };

        WhenReady(() =>
        {
            Scores(mode).Push().SetValueAsync(data).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) Debug.LogError("Uloženie skóre zlyhalo: " + t.Exception.GetBaseException().Message);
                else Debug.Log("Skóre uložené do Firebase: " + mode + " " + score);
            });
        }, () => { });
    }

    // done dostane null, ak sa načítanie nepodarí
    public static void LoadTop10(string mode, Action<List<ScoreEntry>> done)
    {
        bool race = mode == Race;

        WhenReady(() =>
        {
            Query query = Scores(mode).OrderByChild("score");
            // shortcut: preteky stiahnu 50 najlepších poradí a podľa času triedia lokálne; pri tisícoch hier treba spoločný kľúč poradie+čas
            query = race ? query.LimitToFirst(50) : query.LimitToLast(10);

            query.GetValueAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.LogError("Načítanie rebríčka zlyhalo: " + t.Exception.GetBaseException().Message);
                    done(null);
                    return;
                }

                IEnumerable<ScoreEntry> list = t.Result.Children.Select(Parse);
                list = race ? list.OrderBy(e => e.score).ThenBy(e => e.time) : list.OrderByDescending(e => e.score);
                done(list.Take(10).ToList());
            });
        }, () => done(null));
    }

    static ScoreEntry Parse(DataSnapshot s)
    {
        return new ScoreEntry
        {
            name = s.Child("name").Value as string ?? "?",
            score = Convert.ToInt32(s.Child("score").Value),
            time = Convert.ToSingle(s.Child("time").Value),
            level = Convert.ToInt32(s.Child("level").Value),
            date = s.Child("date").Value as string ?? "",
        };
    }
}
