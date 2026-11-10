using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Jednorazové nastavenie: vytvorí scénu MainMenu a nastaví poradie scén v Build Settings
public static class MarcoSetup
{
    const string MenuPath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Marco/Vytvor MainMenu a Build Settings")]
    static void Setup()
    {
        if (!File.Exists(MenuPath))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.1f);
            new GameObject("MainMenu").AddComponent<MainMenuUI>();
            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        string[] scenes =
        {
            MenuPath,                                                   // index 0 = štartovacia scéna
            "Assets/Scenes/" + MainMenuUI.RaceScene + ".unity",
            "Assets/Scenes/" + MainMenuUI.EndlessScene + ".unity",
        };
        EditorBuildSettings.scenes = scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        Debug.Log("Hotovo: MainMenu + Build Settings (" + string.Join(", ", scenes) + ")");
    }
}
