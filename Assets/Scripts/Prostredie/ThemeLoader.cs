using UnityEngine;

// Vloží do scény tému prostredia. Marco: PlayerPrefs "Theme" = 0 Mesto, 1 Púšť, 2 Noc; -1 (predvolené) = podľa levelu.
public class ThemeLoader : MonoBehaviour
{
    public const string ThemeKey = "Theme";
    public static readonly string[] Names = { "Mesto", "Púšť", "Noc" };

    public GameObject[] themes;           // poradie ako Names
    public int testTheme = -1;            // len na testovanie v editore, -1 = vypnuté

    void Awake()
    {
        int index = testTheme >= 0 ? testTheme : PlayerPrefs.GetInt(ThemeKey, -1);
        if (index < 0) index = GameSession.CurrentLevel - 1;   // level 1 Mesto, 2 Púšť, 3 Noc, 4 Mesto...
        Instantiate(themes[((index % themes.Length) + themes.Length) % themes.Length], transform);
    }
}
