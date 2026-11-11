using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

// Samo: grafika a prostredie v štýle "koberec s cestou v detskej izbe".
// Všetko sa generuje kódom: textúry, materiály, úseky cesty, štart/cieľ, autíčka, efekty, scéna Test_Samo.
// Spustenie znova je bezpečné: assety sa prepíšu na tom istom mieste (GUID ostáva, odkazy sa nerozbijú).
public static class SamoSetup
{
    const string Art = "Assets/Art/Prostredie";
    const string PrefabDir = "Assets/Prefabs/Prostredie";
    const string TestScene = "Assets/Scenes/Test_Samo.unity";
    const float SegLen = 40f;
    const int Variants = 6;
    const float WallX = 52f;              // vnútorná strana steny izby
    const float WallH = 60f;

    const int Matte = 0, Plastic = 1, Glow = 2;

    static Texture2D texRoad, texFuzz, texWood, texChecker, texSoft, texBlock, texBall;
    static Mesh ball;                     // guľa s menej trojuholníkmi než vstavaná (priemer 1)
    static readonly HashSet<string> made = new HashSet<string>();   // čo tento beh vygeneroval; zvyšok sú siroty zo starších verzií

    static readonly Vector3 V0 = Vector3.zero, V1 = Vector3.one;
    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    // ───────────────────────── menu ─────────────────────────

    [MenuItem("Tools/Samo/1. Vygeneruj prostredie, autá a Test_Samo")]
    public static void GenerateAll()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string d in new[] { Art + "/Textures", Art + "/Materials", Art + "/Meshes", PrefabDir })
            Directory.CreateDirectory(d);
        AssetDatabase.Refresh();
        made.Clear();

        float[] arc = new float[18];
        for (int i = 0; i < 9; i++)
        {
            float a = (-90f + i * 22.5f) * Mathf.Deg2Rad;
            arc[i * 2] = 0.5f * Mathf.Cos(a);
            arc[i * 2 + 1] = 0.5f * Mathf.Sin(a);
        }
        arc[0] = arc[16] = 0f;
        ball = Lathe(16, arc);

        BuildTextures();
        GameObject[] themes = { BuildTheme(NewPal("Mesto")), BuildTheme(NewPal("Pust")), BuildTheme(NewPal("Noc")) };
        GameObject loader = BuildLoader(themes);
        GameObject start = BuildLine("StartLine", false);
        GameObject finish = BuildLine("FinishLine", true);
        BuildCars();
        BuildTestScene(loader, start, finish);
        RemoveOrphans();

        AssetDatabase.SaveAssets();
        Debug.Log("Samo: hotovo. Otvor Assets/Scenes/Test_Samo.unity a daj Play.");
    }

    // Pre toho, kto má práve hlavnú scénu: pridá prostredie, skryje dočasnú cestu, vymení štart a cieľ
    [MenuItem("Tools/Samo/2. Pripoj prostredie do otvorenej scény")]
    public static void AttachToOpenScene()
    {
        GameObject loaderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Prostredie.prefab");
        if (loaderPrefab == null) { Debug.LogError("Najprv spusti Tools/Samo/1."); return; }

        Scene scene = SceneManager.GetActiveScene();
        if (Object.FindAnyObjectByType<ThemeLoader>() != null) { Debug.Log("Samo: prostredie už v scéne je."); return; }
        Undo.RegisterCreatedObjectUndo(PrefabUtility.InstantiatePrefab(loaderPrefab), "Prostredie");

        // dočasná cesta = Plane v koreni scény
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Plane" && go.GetComponent<CarController>() == null)
            {
                Undo.RecordObject(go, "Skry cestu");
                go.SetActive(false);
                Debug.Log("Samo: skrytá dočasná cesta " + go.name);
            }
        }

        RaceManager race = Object.FindAnyObjectByType<RaceManager>();
        if (race != null)
        {
            GameObject fin = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/FinishLine.prefab"));
            fin.transform.position = new Vector3(0f, 0f, race.finishZ);
            Undo.RegisterCreatedObjectUndo(fin, "Ciel");
            Undo.RecordObject(race, "Ciel");
            if (race.finishLine != null) { Undo.RecordObject(race.finishLine.gameObject, "Ciel"); race.finishLine.gameObject.SetActive(false); }
            race.finishLine = fin.transform;   // RaceManager si ju sám posunie na finishZ podľa levelu

            GameObject oldStart = GameObject.Find("StartLine");
            GameObject st = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/StartLine.prefab"));
            st.transform.position = new Vector3(0f, 0f, oldStart != null ? oldStart.transform.position.z : 0f);
            Undo.RegisterCreatedObjectUndo(st, "Start");
            if (oldStart != null) { Undo.RecordObject(oldStart, "Start"); oldStart.SetActive(false); }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Samo: prostredie pripojené do " + scene.name + ". Skontroluj a ulož scénu (Ctrl+S).");
    }

    // Pripojí prostredie do herných scén z menu (preteky aj nekonečný) a uloží ich.
    // Batchmode: Unity.exe -batchmode -projectPath . -executeMethod SamoSetup.AttachToGameScenes -quit
    [MenuItem("Tools/Samo/3. Pripoj prostredie do herných scén")]
    public static void AttachToGameScenes()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string name in new[] { MainMenuUI.RaceScene, MainMenuUI.EndlessScene })
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
            AttachToOpenScene();
            EditorSceneManager.SaveScene(scene);
        }
    }

    // zmaže generované assety, ktoré tento beh už nevytvoril (staré úseky, materiály, meshe)
    static void RemoveOrphans()
    {
        foreach (string guid in AssetDatabase.FindAssets("", new[] { Art, PrefabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            bool generated = path.StartsWith(Art) || Path.GetFileName(path).StartsWith("RoadSegment_");
            if (generated && !AssetDatabase.IsValidFolder(path) && !made.Contains(path))
                AssetDatabase.DeleteAsset(path);
        }
    }

    // ───────────────────────── textúry ─────────────────────────

    static float H(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    static float Fuzz(int x, int y) { return 0.8f + 0.2f * (0.6f * H(x, y) + 0.4f * H(x >> 1, y >> 1)); }

    static void BuildTextures()
    {
        // cesta 8 m × 10 m (32 px na meter): sivý koberec, žltá stredová čiara, biele pruhy a okraje
        texRoad = Tex("Cesta", 256, 320, (x, y) =>
        {
            float mx = x / 32f - 4f, mz = y / 32f, n = Fuzz(x, y);
            bool dash = (mz + 2.5f) % 10f < 5f;   // súmerné okolo začiatku úseku: sedí aj na úseku otočenom o 180°
            Color c = new Color(0.6f, 0.6f, 0.62f);
            if (dash && Mathf.Abs(mx) < 0.14f) c = new Color(1f, 0.78f, 0.15f);
            else if (dash && Mathf.Abs(Mathf.Abs(mx) - 2f) < 0.09f) c = new Color(0.97f, 0.97f, 0.95f);
            else if (Mathf.Abs(Mathf.Abs(mx) - 3.72f) < 0.11f) c = new Color(0.97f, 0.97f, 0.95f);
            return c * n;
        });
        texFuzz = Tex("Koberec", 128, 128, (x, y) => Color.white * Fuzz(x, y));
        texWood = Tex("Parkety", 128, 128, (x, y) =>
        {
            int plank = x / 32;
            float v = 0.85f + 0.15f * H(plank, 7) + 0.04f * Mathf.Sin(y * 0.35f + H(plank, 3) * 20f);
            if (x % 32 == 0 || (y + plank * 45) % 128 == 0) v *= 0.6f;
            return Color.white * v;
        });
        texChecker = Tex("Sachovnica", 64, 64, (x, y) => ((x / 32) + (y / 32)) % 2 == 0 ? Color.white : new Color(0.08f, 0.08f, 0.08f));
        texSoft = Tex("Castica", 64, 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        }, true);
        // drevená kocka: tmavší okraj a svetlý rámik ako na kockách s písmenkami
        texBlock = Tex("Kocka", 64, 64, (x, y) =>
        {
            int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(63 - x, 63 - y));
            return Color.white * (d < 5 ? 0.72f : d < 8 ? 1f : 0.9f);
        });
        // plážová lopta: farebné pásy okolo
        Color[] bands = { new Color(0.9f, 0.2f, 0.15f), Color.white, new Color(0.2f, 0.45f, 0.9f), Color.white, new Color(1f, 0.8f, 0.15f), Color.white };
        texBall = Tex("Lopta", 96, 8, (x, y) => bands[x / 16]);
    }

    static readonly float[] PuffY = { 0f, 0.26f, 0.42f, 0.24f, 0f };
    static readonly float[] PuffR = { 0.32f, 0.44f, 0.55f, 0.42f, 0.3f };

    // Oblaky na tapete; u, v sú v dlaždiciach (vzor sa opakuje po 1). Vráti krytie 0..1, shade: 0 = spodok oblaku, 1 = vrch.
    static float Cloud(float u, float v, out float shade)
    {
        float cover = 0f;
        shade = 1f;
        for (int c = 0; c < 5; c++)
        {
            float s = 0.09f + 0.07f * H(c, 9);
            float cx = H(c * 7 + 1, 3), cy = (c + 0.25f + 0.5f * H(c, 5)) / 5f;
            float dy = Mathf.Repeat(v - cy + 0.5f, 1f) - 0.5f;
            float bottom = (dy + 0.2f * s) / 0.004f;   // rovný spodok
            if (bottom <= 0f || dy > s) continue;
            for (int k = 0; k < 5; k++)
            {
                float dx = Mathf.Repeat(u - cx - (k - 2) * s * 0.45f + 0.5f, 1f) - 0.5f;
                float py = dy - PuffY[k] * s;
                float a = Mathf.Clamp01((PuffR[k] * s - Mathf.Sqrt(dx * dx + py * py)) / 0.004f) * Mathf.Clamp01(bottom);
                if (a > cover)
                {
                    cover = a;
                    shade = Mathf.Clamp01((dy + 0.2f * s) / (0.6f * s));
                }
            }
        }
        return cover;
    }

    static Texture2D Wallpaper(string name, Color bg, Color cloud, Color shadow)
    {
        return Tex(name, 512, 512, (x, y) =>
        {
            float sh;
            float a = Cloud(x / 512f, y / 512f, out sh);
            return Color.Lerp(bg, Color.Lerp(shadow, cloud, sh), a);
        });
    }

    // 360° panoráma namiesto oblohy: vzdialená stena s tou istou tapetou a strop (horizont v strede)
    static Texture2D Panorama(string name, Color bg, Color cloud, Color shadow, Color ceiling, bool stars)
    {
        return Tex(name, 1024, 512, (x, y) =>
        {
            float v = y / 511f;
            if (v > 0.9f) return ceiling;
            if (v > 0.89f) return Color.Lerp(ceiling, bg, 0.35f);   // lišta pod stropom
            float sh;
            float a = Cloud(x / 128f, y / 128f, out sh) * Mathf.Clamp01((v - 0.52f) / 0.05f);
            Color c = Color.Lerp(bg, Color.Lerp(shadow, cloud, sh), a);
            if (stars && v > 0.5f && H(x, y) > 0.997f) c = new Color(1f, 0.95f, 0.6f);
            return c;
        });
    }

    static Texture2D Tex(string name, int w, int h, Func<int, int, Color> px, bool clamp = false)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] cols = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = px(x, y);
                if (!clamp) c.a = 1f;
                cols[y * w + x] = c;
            }
        t.SetPixels(cols);
        string path = Art + "/Textures/" + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        made.Add(path);

        AssetDatabase.ImportAsset(path);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        imp.anisoLevel = 4;
        imp.mipmapEnabled = true;
        imp.alphaIsTransparency = clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────────────────────── materiály ─────────────────────────

    static T Save<T>(T obj, string path) where T : Object
    {
        made.Add(path);
        obj.name = Path.GetFileNameWithoutExtension(path);
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }
        EditorUtility.CopySerialized(obj, existing);   // zachová GUID
        Object.DestroyImmediate(obj);
        return existing;
    }

    // Matte: Simple Lit (lacné pre mobil), Plastic: Lit s leskom (hračky, autá), Glow: Unlit (svieti aj v noci)
    static Material Mat(string name, Color color, Texture2D tex = null, int kind = Matte)
    {
        string shader = kind == Glow ? "Universal Render Pipeline/Unlit"
                      : kind == Plastic ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Simple Lit";
        Material m = new Material(Shader.Find(shader));
        m.SetColor("_BaseColor", color);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", kind == Plastic ? 0.6f : 0.1f);
        m.enableInstancing = true;
        return Save(m, Art + "/Materials/" + name + ".mat");
    }

    static Material ParticleMat(string name, bool additive)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", texSoft);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return Save(m, Art + "/Materials/" + name + ".mat");
    }

    static readonly string[] ToyNames = { "Cervena", "Zlta", "Modra", "Zelena", "Oranzova", "Fialova" };
    static readonly Color[] ToyColors =
    {
        new Color(0.9f, 0.2f, 0.15f), new Color(1f, 0.8f, 0.15f), new Color(0.2f, 0.45f, 0.9f),
        new Color(0.25f, 0.7f, 0.3f), new Color(1f, 0.5f, 0.1f), new Color(0.6f, 0.35f, 0.8f),
    };

    class Pal
    {
        public string name;
        public bool night, desert;
        public Color bg, cloud, shadow, ceiling;   // tapeta
        // spoločné pre všetky témy
        public Material road, floor, white, wood, dark, teddy, teddyLight, ballMat, basket, hat, gold, lampHead, star;
        public Material[] blocks, colors;          // kocky s rámikom, lesklé plastové farby
        // podľa témy
        public Material wall, rug, rugEdge, curb, pane, curtain, houseWindow;
        // domčeky pri ceste / púšť
        public Material door, trunk, fence, lampPost, castle, cactus;
        public Material[] house, roof, leaves;
    }

    static Pal NewPal(string name)
    {
        Pal p = new Pal { name = name, night = name == "Noc", desert = name == "Pust" };

        p.road = Mat("Cesta", Color.white, texRoad);
        p.floor = Mat("Parkety", new Color(0.82f, 0.6f, 0.38f), texWood);
        p.white = Mat("Biela", new Color(0.97f, 0.96f, 0.93f));
        p.wood = Mat("Drevo", new Color(0.62f, 0.43f, 0.27f), texWood);
        p.dark = Mat("Tmava", new Color(0.16f, 0.16f, 0.18f));
        p.teddy = Mat("Medved", new Color(0.72f, 0.5f, 0.3f), texFuzz);
        p.teddyLight = Mat("Medved_Svetly", new Color(0.93f, 0.8f, 0.6f), texFuzz);
        p.ballMat = Mat("Lopta", Color.white, texBall, Plastic);
        p.basket = Mat("Kosik", new Color(0.8f, 0.62f, 0.38f), texFuzz);
        p.hat = Mat("Klobuk", new Color(0.66f, 0.45f, 0.27f), texFuzz);
        p.gold = Mat("Zlata", new Color(0.95f, 0.75f, 0.25f), null, Plastic);
        p.lampHead = Mat("LampaSvetlo", new Color(1f, 0.93f, 0.7f), null, Glow);
        p.star = Mat("Hviezda", new Color(1f, 0.95f, 0.5f), null, Glow);
        p.trunk = Mat("Kmen", new Color(0.5f, 0.33f, 0.2f));
        p.blocks = new Material[ToyColors.Length];
        p.colors = new Material[ToyColors.Length];
        for (int i = 0; i < ToyColors.Length; i++)
        {
            p.blocks[i] = Mat("Kocka_" + ToyNames[i], ToyColors[i], texBlock);
            p.colors[i] = Mat("Farba_" + ToyNames[i], ToyColors[i], null, Plastic);
        }

        Color rug, edge, curb, pane, curtain;
        if (p.desert)
        {
            p.bg = new Color(1f, 0.74f, 0.5f); p.cloud = new Color(1f, 0.96f, 0.9f); p.shadow = new Color(1f, 0.84f, 0.72f); p.ceiling = new Color(1f, 0.97f, 0.92f);
            rug = new Color(0.95f, 0.82f, 0.56f); edge = new Color(0.9f, 0.45f, 0.2f); curb = new Color(1f, 0.55f, 0.15f);
            pane = new Color(1f, 0.85f, 0.55f); curtain = new Color(0.9f, 0.4f, 0.35f);
            p.castle = Mat("Pust_Hrad", new Color(0.88f, 0.72f, 0.48f));
            p.cactus = Mat("Pust_Kaktus", new Color(0.33f, 0.66f, 0.32f));
            p.leaves = new[] { Mat("Pust_Palma", new Color(0.3f, 0.7f, 0.3f)) };
            p.fence = Mat("Pust_Plot", new Color(0.7f, 0.5f, 0.3f));
        }
        else
        {
            if (p.night)
            {
                p.bg = new Color(0.1f, 0.13f, 0.3f); p.cloud = new Color(0.27f, 0.31f, 0.53f); p.shadow = new Color(0.18f, 0.21f, 0.4f); p.ceiling = new Color(0.07f, 0.08f, 0.17f);
                rug = new Color(0.22f, 0.32f, 0.6f); edge = new Color(0.5f, 0.35f, 0.75f); curb = new Color(1f, 0.82f, 0.2f);
                pane = new Color(0.05f, 0.07f, 0.2f); curtain = new Color(0.42f, 0.36f, 0.72f);
            }
            else
            {
                p.bg = new Color(0.36f, 0.64f, 0.94f); p.cloud = Color.white; p.shadow = new Color(0.8f, 0.88f, 0.98f); p.ceiling = new Color(0.98f, 0.98f, 1f);
                rug = new Color(0.38f, 0.66f, 0.27f); edge = new Color(0.85f, 0.25f, 0.2f); curb = new Color(1f, 0.82f, 0.2f);
                pane = new Color(0.86f, 0.95f, 1f); curtain = new Color(1f, 0.85f, 0.35f);
            }
            p.house = new[]
            {
                Mat("Dom_Ruzovy", new Color(1f, 0.72f, 0.72f)), Mat("Dom_Broskyna", new Color(1f, 0.83f, 0.6f)),
                Mat("Dom_Modry", new Color(0.62f, 0.8f, 1f)), Mat("Dom_Krem", new Color(1f, 0.96f, 0.78f)),
            };
            p.roof = new[]
            {
                Mat("Strecha_Fialova", new Color(0.45f, 0.35f, 0.72f)), Mat("Strecha_Cervena", new Color(0.85f, 0.27f, 0.2f)),
                Mat("Strecha_Modra", new Color(0.25f, 0.4f, 0.8f)),
            };
            p.houseWindow = p.night ? Mat("Okno_Svieti", new Color(1f, 0.85f, 0.4f), null, Glow) : Mat("Okno", new Color(0.5f, 0.75f, 0.95f));
            p.door = Mat("Dvere", new Color(0.55f, 0.32f, 0.2f));
            p.leaves = new[] { Mat("Lista1", new Color(0.3f, 0.68f, 0.25f)), Mat("Lista2", new Color(0.48f, 0.8f, 0.3f)) };
            p.fence = Mat("Plot", new Color(0.98f, 0.98f, 0.96f));
            p.lampPost = Mat("Lampa", new Color(0.3f, 0.32f, 0.38f));
        }

        p.wall = Mat(name + "_Tapeta", Color.white, Wallpaper("Tapeta_" + name, p.bg, p.cloud, p.shadow));
        p.rug = Mat(name + "_Koberec", rug, texFuzz);
        p.rugEdge = Mat(name + "_Lem", edge, texFuzz);
        p.curb = Mat(name + "_Obrubnik", curb);
        p.pane = Mat(name + "_Sklo", pane, null, Glow);
        p.curtain = Mat(name + "_Zaves", curtain, texFuzz);
        return p;
    }

    // ───────────────────────── geometria ─────────────────────────

    // konvexný mesh z polygónov; normály von, UV v metroch (textúra nadväzuje bez ohľadu na veľkosť)
    static Mesh Poly(List<Vector3[]> faces, float uvMeters)
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tri = new List<int>();

        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (Vector3[] f in faces) foreach (Vector3 q in f) { center += q; count++; }
        center /= count;

        foreach (Vector3[] f in faces)
        {
            Vector3[] p = f;
            Vector3 fc = Vector3.zero;
            foreach (Vector3 q in f) fc += q;
            fc /= f.Length;
            Vector3 n = Vector3.Cross(f[1] - f[0], f[2] - f[0]);
            if (Vector3.Dot(n, fc - center) < 0f) { p = (Vector3[])f.Clone(); Array.Reverse(p); n = -n; }

            Vector3 an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            Vector3 ua, va;
            if (an.y >= an.x && an.y >= an.z) { ua = Vector3.right; va = Vector3.forward; }
            else if (an.x >= an.z) { ua = Vector3.forward; va = Vector3.up; }
            else { ua = Vector3.right; va = Vector3.up; }

            int b = v.Count;
            foreach (Vector3 q in p)
            {
                v.Add(q);
                uv.Add(new Vector2(Vector3.Dot(q, ua), Vector3.Dot(q, va)) / uvMeters);
            }
            for (int i = 1; i < p.Length - 1; i++) { tri.Add(b); tri.Add(b + i); tri.Add(b + i + 1); }
        }

        Mesh m = new Mesh();
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static Mesh BoxMesh(Vector3 s, float uvMeters)
    {
        Vector3 h = s * 0.5f;
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++)
            c[i] = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
        return Poly(new List<Vector3[]>
        {
            new[] { c[0], c[1], c[3], c[2] }, new[] { c[4], c[5], c[7], c[6] },
            new[] { c[0], c[2], c[6], c[4] }, new[] { c[1], c[3], c[7], c[5] },
            new[] { c[0], c[1], c[5], c[4] }, new[] { c[2], c[3], c[7], c[6] },
        }, uvMeters);
    }

    // mnohouholník v rovine XY (konvexný, páry x, y) vytiahnutý pozdĺž Z
    static Mesh Prism(float depth, params float[] xy)
    {
        int n = xy.Length / 2;
        Vector3[] a = new Vector3[n], b = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = new Vector3(xy[i * 2], xy[i * 2 + 1], -depth / 2f);
            b[i] = new Vector3(xy[i * 2], xy[i * 2 + 1], depth / 2f);
        }
        List<Vector3[]> faces = new List<Vector3[]> { a, b };
        for (int i = 0; i < n; i++)
            faces.Add(new[] { a[i], a[(i + 1) % n], b[(i + 1) % n], b[i] });
        return Poly(faces, 1f);
    }

    // rotačné teleso okolo osi Y z profilu (páry polomer, výška). Profil ide zospodu po vonkajšej strane hore, potom je povrch otočený von.
    static Mesh Lathe(int sides, params float[] ry)
    {
        int n = ry.Length / 2;
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tri = new List<int>();
        for (int i = 0; i < n; i++)
            for (int s = 0; s <= sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                v.Add(new Vector3(Mathf.Cos(a) * ry[i * 2], ry[i * 2 + 1], Mathf.Sin(a) * ry[i * 2]));
                uv.Add(new Vector2(s / (float)sides, i / (float)(n - 1)));
            }
        for (int i = 0; i < n - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = i * (sides + 1) + s, b = a + sides + 1;
                tri.Add(a); tri.Add(b); tri.Add(a + 1);
                tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
            }

        Mesh m = new Mesh();
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static GameObject Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler)
    {
        GameObject go = new GameObject(mesh.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Part(Transform parent, Mesh mesh, Material mat) { return Part(parent, mesh, mat, V0, V1, V0); }

    // prázdny rodič: hračka sa poskladá v jeho súradniciach a on ju posunie, otočí a zväčší
    static Transform Group(Transform parent, Vector3 pos, Vector3 euler, float scale)
    {
        Transform t = new GameObject("g").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localEulerAngles = euler;
        t.localScale = V1 * scale;
        return t;
    }

    static GameObject Box(Transform parent, Material mat, Vector3 pos, Vector3 size, float uvMeters = 2f, float rotY = 0f)
    {
        return Part(parent, BoxMesh(size, uvMeters), mat, pos, V1, new Vector3(0f, rotY, 0f));
    }

    static Mesh Builtin(string name) { return Resources.GetBuiltinResource<Mesh>(name + ".fbx"); }

    static GameObject Shape(Transform parent, string builtin, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default(Vector3))
    {
        return Part(parent, Builtin(builtin), mat, pos, scale, euler);
    }

    // valec s priemerom d a výškou h (os Y)
    static GameObject Cyl(Transform parent, Material mat, Vector3 pos, float d, float h, Vector3 euler = default(Vector3))
    {
        return Shape(parent, "Cylinder", mat, pos, new Vector3(d, h / 2f, d), euler);
    }

    static GameObject Ball(Transform parent, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default(Vector3))
    {
        return Part(parent, ball, mat, pos, scale, euler);
    }

    static GameObject Ball(Transform parent, Material mat, Vector3 pos, float d) { return Part(parent, ball, mat, pos, V1 * d, V0); }

    static void Roof(Transform parent, Material mat, Vector3 basePos, float w, float h, float d)
    {
        Part(parent, Prism(d, -w / 2f, 0f, w / 2f, 0f, 0f, h), mat, basePos, V1, V0);
    }

    // zlúči všetky časti podľa materiálu: zo stoviek objektov na úsek ostane pár desiatok (menej draw callov)
    static void Combine(GameObject root, string prefix)
    {
        Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
        Matrix4x4 toLocal = root.transform.worldToLocalMatrix;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
        {
            Material mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
            List<CombineInstance> list;
            if (!groups.TryGetValue(mat, out list)) groups[mat] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
        }
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        int n = 0;
        foreach (KeyValuePair<Material, List<CombineInstance>> g in groups)
        {
            Mesh m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.CombineMeshes(g.Value.ToArray(), true, true);
            m.RecalculateBounds();
            m = Save(m, Art + "/Meshes/" + prefix + "_" + (n++) + ".asset");
            GameObject go = new GameObject(g.Key.name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = g.Key;
            if (g.Key.shader.name.EndsWith("Unlit")) r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static float R(Random rnd, float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
    static T Pick<T>(Random rnd, T[] arr) { return arr[rnd.Next(arr.Length)]; }

    static List<int> Shuffled(Random rnd, int n)
    {
        List<int> l = new List<int>();
        for (int i = 0; i < n; i++) l.Insert(rnd.Next(l.Count + 1), i);
        return l;
    }

    // ───────────────────────── hračky ─────────────────────────

    static void Rocket(Transform r, Pal p, Random rnd, Vector3 pos, float h)
    {
        Transform g = Group(r, pos, V(0f, R(rnd, 0f, 360f), 0f), h);
        Material c = Pick(rnd, p.colors);
        Part(g, Lathe(14, 0f, 0.15f, 0.12f, 0.15f, 0.16f, 0.3f, 0.16f, 0.6f, 0.14f, 0.7f), p.white);
        Part(g, Lathe(14, 0.14f, 0.7f, 0.1f, 0.84f, 0.05f, 0.94f, 0f, 1f), c);
        Part(g, Lathe(14, 0.164f, 0.34f, 0.164f, 0.42f), c);
        Cyl(g, p.dark, V(0f, 0.1f, 0f), 0.16f, 0.1f);
        Ball(g, p.colors[2], V(0f, 0.55f, 0.15f), V(0.1f, 0.1f, 0.04f));
        Mesh fin = Prism(0.03f, 0.13f, 0.14f, 0.3f, 0f, 0.14f, 0.4f);
        for (int k = 0; k < 3; k++) Part(g, fin, c, V0, V1, V(0f, k * 120f, 0f));
    }

    static void BlockTower(Transform r, Pal p, Random rnd, Vector3 pos, float b, int n)
    {
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float s = b * R(rnd, 0.85f, 1f);
            Box(r, Pick(rnd, p.blocks), pos + V(R(rnd, -0.12f, 0.12f) * b, y + s / 2f, R(rnd, -0.12f, 0.12f) * b), V1 * s, s, R(rnd, -20f, 20f));
            y += s;
        }
        Vector3 top = pos + Vector3.up * y;
        switch (rnd.Next(3))
        {
            case 0: Part(r, Lathe(12, 0f, 0f, 0.5f, 0f, 0f, 1f), Pick(rnd, p.colors), top, V1 * b, V0); break;                 // kužeľ
            case 1: Part(r, Prism(b, -b / 2f, 0f, b / 2f, 0f, 0f, b * 0.7f), Pick(rnd, p.blocks), top, V1, V0); break;         // strieška
            default: Cyl(r, Pick(rnd, p.colors), top + Vector3.up * b * 0.4f, b * 0.9f, b * 0.8f); break;
        }
    }

    static void BlockPile(Transform r, Pal p, Random rnd, Vector3 pos)
    {
        for (int k = 0; k < 7; k++)
        {
            float s = R(rnd, 1.6f, 2.4f);
            Vector3 q = pos + V(R(rnd, -4f, 4f), s / 2f, R(rnd, -4f, 4f));
            Box(r, Pick(rnd, p.blocks), q, V1 * s, s, R(rnd, 0f, 90f));
            if (k < 2) Box(r, Pick(rnd, p.blocks), q + Vector3.up * s, V1 * s, s, R(rnd, 0f, 90f));
        }
    }

    // krúžky na tyči
    static void Stacker(Transform r, Pal p, Vector3 pos, float h)
    {
        Transform g = Group(r, pos, V0, h);
        Cyl(g, p.wood, V(0f, 0.04f, 0f), 0.7f, 0.08f);
        Cyl(g, p.wood, V(0f, 0.5f, 0f), 0.07f, 1f);
        for (int k = 0; k < 5; k++)
            Ball(g, p.colors[k], V(0f, 0.17f + k * 0.16f, 0f), V(0.66f - k * 0.09f, 0.17f, 0.66f - k * 0.09f));
        Ball(g, p.colors[5], V(0f, 1f, 0f), 0.16f);
    }

    // sediaci medvedík, pozerá v smere +Z
    static void Teddy(Transform r, Pal p, Vector3 pos, float h, float rotY)
    {
        Transform g = Group(r, pos, V(0f, rotY, 0f), h);
        Material a = p.teddy, b = p.teddyLight;
        Ball(g, a, V(0f, 0.3f, 0f), V(0.56f, 0.6f, 0.5f));
        Ball(g, b, V(0f, 0.3f, 0.17f), V(0.34f, 0.38f, 0.2f));
        Ball(g, a, V(0f, 0.74f, 0.02f), 0.44f);
        Ball(g, b, V(0f, 0.69f, 0.2f), V(0.2f, 0.15f, 0.14f));
        Ball(g, p.dark, V(0f, 0.73f, 0.265f), 0.055f);
        for (int x = -1; x <= 1; x += 2)
        {
            Ball(g, a, V(x * 0.17f, 0.95f, 0f), 0.16f);
            Ball(g, p.dark, V(x * 0.09f, 0.81f, 0.2f), 0.045f);
            Ball(g, a, V(x * 0.32f, 0.42f, 0.08f), V(0.17f, 0.34f, 0.17f), V(0f, 0f, x * 35f));
            Ball(g, a, V(x * 0.19f, 0.09f, 0.27f), V(0.2f, 0.18f, 0.36f));
        }
    }

    // drevený vláčik: rušeň a dva vagóny s nákladom
    static void Train(Transform r, Pal p, Random rnd, Vector3 pos, float rotY)
    {
        Transform g = Group(r, pos, V(0f, rotY, 0f), 1.9f);
        const float o = 2.5f;   // posun, aby bol vláčik vycentrovaný
        Box(g, p.colors[0], V(0f, 0.45f, o), V(1f, 0.2f, 2.2f));
        Cyl(g, p.colors[2], V(0f, 0.95f, o + 0.4f), 0.8f, 1.3f, V(90f, 0f, 0f));
        Box(g, p.colors[3], V(0f, 1.05f, o - 0.65f), V(0.95f, 1f, 0.8f));
        Box(g, p.colors[1], V(0f, 1.6f, o - 0.65f), V(1.1f, 0.1f, 0.95f));
        Cyl(g, p.dark, V(0f, 1.5f, o + 0.75f), 0.25f, 0.4f);
        for (int w = 1; w <= 2; w++)
        {
            float z = o - w * 2.5f;
            Box(g, p.colors[w + 3], V(0f, 0.45f, z), V(1f, 0.2f, 2f));
            Box(g, Pick(rnd, p.blocks), V(0f, 0.95f, z), V(0.8f, 0.8f, 1.4f), 0.8f);
            Box(g, p.dark, V(0f, 0.45f, z + 1.15f), V(0.12f, 0.08f, 0.5f));
        }
        foreach (float z in new[] { 0.7f, -0.7f, -1.8f, -3.2f, -4.3f, -5.7f })
            for (int x = -1; x <= 1; x += 2)
                Cyl(g, p.dark, V(x * 0.52f, 0.25f, o + z), 0.5f, 0.08f, V(0f, 0f, 90f));
    }

    static void Crayons(Transform r, Pal p, Random rnd, Vector3 pos)
    {
        Mesh tip = Lathe(10, 0.45f, 0f, 0.12f, 1.1f, 0f, 1.1f);
        for (int k = 0; k < 4; k++)
        {
            Transform g = Group(r, pos + V(R(rnd, -3f, 3f), 0.45f, R(rnd, -3f, 3f)), V(0f, R(rnd, 0f, 180f), 0f), 1f);
            Material c = p.colors[(k * 2 + rnd.Next(2)) % p.colors.Length];
            Cyl(g, c, V0, 0.9f, 5f, V(90f, 0f, 0f));
            Part(g, tip, c, V(0f, 0f, 2.5f), V1, V(90f, 0f, 0f));
            Cyl(g, p.white, V(0f, 0f, -0.6f), 0.93f, 1.6f, V(90f, 0f, 0f));
        }
    }

    static void Books(Transform r, Pal p, Random rnd, Vector3 pos)
    {
        int n = rnd.Next(3, 6);
        for (int i = 0; i < n; i++)
            Box(r, Pick(rnd, p.colors), pos + V(R(rnd, -0.5f, 0.5f), 0.75f + i * 1.5f, R(rnd, -0.5f, 0.5f)),
                V(R(rnd, 7f, 9f), 1.5f, R(rnd, 5f, 6.5f)), 2f, R(rnd, -25f, 25f));
    }

    // kolky a guľa
    static void Pins(Transform r, Pal p, Random rnd, Vector3 pos)
    {
        Mesh pin = Lathe(10, 0f, 0f, 0.1f, 0f, 0.14f, 0.2f, 0.13f, 0.42f, 0.06f, 0.62f, 0.07f, 0.72f, 0.085f, 0.82f, 0.06f, 0.94f, 0f, 1f);
        Mesh band = Lathe(10, 0.066f, 0.62f, 0.076f, 0.72f);
        for (int row = 0; row < 3; row++)
            for (int k = 0; k <= row; k++)
            {
                Vector3 q = pos + V((k - row / 2f) * 1.7f, 0f, row * 1.5f);
                Part(r, pin, p.white, q, V1 * 4.2f, V0);
                Part(r, band, p.colors[0], q, V1 * 4.2f, V0);
            }
        Ball(r, p.colors[2], pos + V(R(rnd, -2f, 2f), 1.1f, -4f), 2.2f);
    }

    static void Drum(Transform r, Pal p, Vector3 pos)
    {
        Cyl(r, p.colors[0], pos + V(0f, 1.75f, 0f), 6f, 3.5f);
        Cyl(r, p.white, pos + V(0f, 3.5f, 0f), 6.2f, 0.4f);
        Cyl(r, p.white, pos + V(0f, 0.2f, 0f), 6.2f, 0.4f);
        for (int x = -1; x <= 1; x += 2)
        {
            Cyl(r, p.wood, pos + V(x * 1f, 3.85f, 0f), 0.3f, 4.5f, V(90f, x * 20f, 0f));
            Ball(r, p.wood, pos + V(x * 1.75f, 3.85f, 2.1f), 0.7f);
        }
    }

    // klobúk, polomer strechy = radius; os je lokálne Y
    static void Hat(Transform r, Pal p, Vector3 pos, float radius, Vector3 euler)
    {
        Transform g = Group(r, pos, euler, radius);
        Part(g, Lathe(16, 0f, 0f, 0.6f, 0f, 1f, 0.12f, 1.02f, 0.17f, 0.62f, 0.07f, 0.48f, 0.09f, 0.42f, 0.5f, 0.3f, 0.58f, 0f, 0.5f), p.hat);
        Part(g, Lathe(16, 0.485f, 0.12f, 0.47f, 0.22f), p.dark);
    }

    delegate void Toy(Transform r, Pal p, Random rnd, Vector3 pos);

    // stredne veľké hračky rozložené po podlahe medzi kobercom a stenou
    static readonly Toy[] Toys =
    {
        (r, p, rnd, pos) => Rocket(r, p, rnd, pos, R(rnd, 10f, 16f)),
        (r, p, rnd, pos) => BlockTower(r, p, rnd, pos, R(rnd, 2.4f, 3.4f), rnd.Next(3, 7)),
        (r, p, rnd, pos) => Stacker(r, p, pos, R(rnd, 6f, 8f)),
        (r, p, rnd, pos) => { float d = R(rnd, 6f, 9f); Ball(r, p.ballMat, pos + Vector3.up * d / 2f, V1 * d, V(R(rnd, 0f, 60f), R(rnd, 0f, 360f), 0f)); },
        (r, p, rnd, pos) => Teddy(r, p, pos, R(rnd, 8f, 11f), (pos.x > 0f ? -135f : 135f) + R(rnd, -20f, 20f)),   // tvárou k ceste a ku kamere
        (r, p, rnd, pos) => Train(r, p, rnd, pos, R(rnd, -15f, 15f)),
        (r, p, rnd, pos) => Books(r, p, rnd, pos),
        (r, p, rnd, pos) => Pins(r, p, rnd, pos),
        (r, p, rnd, pos) => Drum(r, p, pos),
        (r, p, rnd, pos) => Hat(r, p, pos, R(rnd, 4.5f, 5.5f), V0),
        (r, p, rnd, pos) => BlockPile(r, p, rnd, pos),
        (r, p, rnd, pos) => Crayons(r, p, rnd, pos),
    };

    // ───────────────────────── veci pri stene ─────────────────────────
    // g je skupina na stene: lokálne X ide od steny do izby, Z pozdĺž steny, stred úseku je 0

    static void Pictures(Transform g, Pal p, Random rnd, float y, float z0, float z1)
    {
        for (float z = z0 + 5f; z < z1 - 4f; z += R(rnd, 12f, 15f))
        {
            float w = R(rnd, 7f, 10f), h = R(rnd, 6f, 9f);
            Box(g, p.wood, V(0.3f, y, z), V(0.5f, h + 1.2f, w + 1.2f));
            Box(g, Pick(rnd, p.colors), V(0.45f, y, z), V(0.5f, h, w));
            Ball(g, Pick(rnd, p.colors), V(0.65f, y, z), V(0.2f, h * 0.5f, h * 0.5f));
        }
    }

    // šnúra s vlajočkami
    static void Bunting(Transform g, Pal p, float y)
    {
        Box(g, p.white, V(0.5f, y, 0f), V(0.15f, 0.15f, SegLen));
        Mesh flag = Prism(0.1f, -1.2f, 0f, 1.2f, 0f, 0f, -3.2f);
        int i = 0;
        for (float z = -18f; z <= 18f; z += 3f)
            Part(g, flag, p.colors[i++ % p.colors.Length], V(0.5f, y, z), V1, V(0f, 90f, 0f));
    }

    static void Window(Transform g, Pal p, Random rnd)
    {
        const float y0 = 16f, h = 30f, w = 24f;
        Box(g, p.pane, V(0.2f, y0 + h / 2f, 0f), V(0.2f, h, w));
        if (p.night) Ball(g, p.star, V(0.35f, y0 + h * 0.72f, 5f), V(0.2f, 6f, 6f));   // mesiac
        foreach (float z in new[] { -w / 2f, 0f, w / 2f })
            Box(g, p.white, V(0.5f, y0 + h / 2f, z), V(0.8f, h + 1.2f, z == 0f ? 0.8f : 1.2f));
        Box(g, p.white, V(0.5f, y0, 0f), V(0.8f, 1.2f, w + 1.2f));
        Box(g, p.white, V(0.5f, y0 + h * 0.55f, 0f), V(0.8f, 0.8f, w));
        Box(g, p.white, V(0.5f, y0 + h, 0f), V(0.8f, 1.2f, w + 1.2f));
        Box(g, p.white, V(1.6f, y0 - 1f, 0f), V(3.2f, 0.8f, w + 3f));                  // parapet
        for (int s = -1; s <= 1; s += 2)
            Box(g, p.curtain, V(1.2f, y0 + h / 2f - 1f, s * (w / 2f + 3.5f)), V(0.8f, h + 8f, 6f));
        Cyl(g, p.wood, V(1.2f, y0 + h + 3.5f, 0f), 0.6f, w + 16f, V(90f, 0f, 0f));
        // kvetináč na parapete
        Part(g, Lathe(10, 0f, 0f, 0.8f, 0f, 1.1f, 2f, 0f, 2f), p.colors[4], V(1.8f, y0 - 0.6f, -6f), V1, V0);
        Ball(g, p.colors[3], V(1.8f, y0 + 3f, -6f), V(3f, 3.6f, 3f));
    }

    static void Shelf(Transform g, Pal p, Random rnd)
    {
        const float w = 30f, h = 40f, d = 9f;
        for (int s = -1; s <= 1; s += 2) Box(g, p.white, V(d / 2f, h / 2f, s * w / 2f), V(d, h, 0.8f));
        Box(g, p.white, V(0.3f, h / 2f, 0f), V(0.4f, h, w));
        for (int k = 0; k < 4; k++) Box(g, p.white, V(d / 2f, 1f + k * 13f, 0f), V(d, 0.8f, w));

        for (int k = 0; k < 3; k++)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = V(d / 2f, 1.4f + k * 13f, s * 7.5f);   // stred priehradky na polici
                switch (rnd.Next(6))
                {
                    case 0:
                    case 1:   // knihy
                        for (float z = c.z - 6f; z < c.z + 5f;)
                        {
                            float t = R(rnd, 0.8f, 1.6f), bh = R(rnd, 7f, 10.5f);
                            Box(g, Pick(rnd, p.colors), V(c.x + R(rnd, -0.5f, 0.5f), c.y + bh / 2f, z + t / 2f), V(6.5f, bh, t));
                            z += t + 0.05f;
                        }
                        break;
                    case 2: Ball(g, p.ballMat, c + Vector3.up * 4f, V1 * 8f, V(30f, 0f, 20f)); break;
                    case 3: Rocket(g, p, rnd, c, 10.5f); break;
                    case 4:
                        Box(g, Pick(rnd, p.blocks), c + V(0f, 1.75f, -2f), V1 * 3.5f, 3.5f, 10f);
                        Box(g, Pick(rnd, p.blocks), c + V(0f, 1.75f, 2f), V1 * 3.5f, 3.5f, -15f);
                        Box(g, Pick(rnd, p.blocks), c + V(0f, 5.25f, 0f), V1 * 3.5f, 3.5f, 25f);
                        break;
                    default: Teddy(g, p, c, 9f, 90f); break;
                }
            }
        // košík s loptou vedľa police
        Cyl(g, p.basket, V(6f, 3.5f, w / 2f + 6f), 9f, 7f);
        Ball(g, Pick(rnd, p.colors), V(6f, 7.5f, w / 2f + 6f), 6f);
    }

    static void Bed(Transform g, Pal p, Random rnd)
    {
        const float L = 36f, W = 20f, cx = W / 2f + 1f;
        foreach (float z in new[] { -L / 2f + 1.2f, L / 2f - 1.2f })
            foreach (float x in new[] { 2f, W })
                Box(g, p.wood, V(x, 2.5f, z), V(2f, 5f, 2f));
        Box(g, p.wood, V(cx, 6f, 0f), V(W, 2.5f, L));
        Box(g, p.white, V(cx, 8.6f, 0f), V(W - 1f, 3f, L - 1f));                                 // matrac
        Box(g, Pick(rnd, p.blocks), V(cx + 0.3f, 8.9f, -5f), V(W + 0.2f, 3.4f, L - 11f), 4f);   // perina
        Ball(g, p.white, V(cx, 11f, L / 2f - 6f), V(13f, 2.6f, 7f));                             // vankúš
        Box(g, p.wood, V(cx, 12f, L / 2f + 0.2f), V(W, 22f, 1.4f));
        Box(g, p.wood, V(cx, 8f, -L / 2f - 0.2f), V(W, 12f, 1.4f));
        Teddy(g, p, V(cx, 10.4f, L / 2f - 11f), 7f, 180f);
        Pictures(g, p, rnd, 36f, -18f, 18f);
    }

    static void Door(Transform g, Pal p, Random rnd)
    {
        const float w = 20f, h = 46f;
        for (int s = -1; s <= 1; s += 2) Box(g, p.white, V(0.4f, h / 2f, s * (w / 2f + 0.8f)), V(0.9f, h + 1.6f, 1.6f));
        Box(g, p.white, V(0.4f, h + 0.8f, 0f), V(0.9f, 1.6f, w + 3.2f));
        Box(g, p.wood, V(0.3f, h / 2f, 0f), V(0.5f, h, w), 6f);
        foreach (float y in new[] { h * 0.27f, h * 0.72f }) Box(g, p.wood, V(0.6f, y, 0f), V(0.3f, h * 0.36f, w * 0.7f), 3f);
        Ball(g, p.gold, V(1.3f, h * 0.47f, w * 0.38f), 1.6f);
        Hat(g, p, V(0.4f, 34f, -w / 2f - 9f), 5.5f, V(0f, 0f, -90f));   // klobúk na stene
        Pictures(g, p, rnd, 32f, w / 2f + 1f, 20f);
    }

    // truhlica s hračkami
    static void Chest(Transform g, Pal p, Random rnd)
    {
        const float w = 18f, d = 10f, h = 8f, cx = 1f + d / 2f;
        Box(g, p.colors[2], V(cx, h / 2f, 0f), V(d, h, w));
        for (int s = -1; s <= 1; s += 2) Box(g, p.colors[1], V(cx, h / 2f, s * w * 0.3f), V(d + 0.3f, h + 0.3f, 1.2f));
        Box(g, p.colors[2], V(0.6f, h + d / 2f, 0f), V(1f, d, w));   // otvorené veko opreté o stenu
        Ball(g, p.ballMat, V(cx, h + 1.5f, -4f), V1 * 6f, V(40f, 0f, 0f));
        Box(g, Pick(rnd, p.blocks), V(cx, h + 1f, 2.5f), V1 * 3.5f, 3.5f, 30f);
        Rocket(g, p, rnd, V(cx + 1f, h - 3f, 6.5f), 9f);
        Teddy(g, p, V(cx + 9f, 0f, -13f), 9f, 70f);
        Bunting(g, p, 44f);
        Pictures(g, p, rnd, 30f, -20f, 20f);
    }

    static void Desk(Transform g, Pal p, Random rnd)
    {
        const float w = 34f, d = 14f, h = 30f;
        foreach (float z in new[] { -w / 2f + 1.5f, w / 2f - 1.5f })
            foreach (float x in new[] { 2f, d - 1f })
                Box(g, p.wood, V(x, h / 2f, z), V(2f, h, 2f));
        Box(g, p.wood, V(d / 2f + 0.5f, h + 0.75f, 0f), V(d + 1f, 1.5f, w));
        Box(g, p.white, V(d / 2f, h - 5f, w / 2f - 7f), V(d - 1f, 10f, 10f));                    // zásuvka
        Ball(g, p.gold, V(d - 0.3f, h - 5f, w / 2f - 7f), 1.2f);
        // lampa a glóbus na okraji stola
        Cyl(g, p.dark, V(d - 3f, h + 1.75f, -8f), 3f, 0.5f);
        Cyl(g, p.dark, V(d - 3f, h + 5.5f, -8f), 0.4f, 8f);
        Part(g, Lathe(12, 0f, 0f, 2.6f, 0f, 1.2f, 3.2f, 0f, 3.2f), p.lampHead, V(d - 3f, h + 8.5f, -8f), V1, V0);
        Cyl(g, p.wood, V(d - 4f, h + 2.3f, 6f), 1f, 1.8f);
        Ball(g, p.colors[2], V(d - 4f, h + 6f, 6f), 6f);
        // pod stolom
        Ball(g, p.ballMat, V(7f, 3.5f, -5f), V1 * 7f, V(20f, 40f, 0f));
        Box(g, Pick(rnd, p.blocks), V(8f, 2f, 5f), V1 * 4f, 4f, 20f);
        Pictures(g, p, rnd, 46f, -18f, 18f);
    }

    delegate void WallFeature(Transform g, Pal p, Random rnd);
    static readonly WallFeature[] WallFeatures = { Window, Shelf, Bed, Door, Chest, Desk };
    static readonly bool[] Shallow = { true, true, false, true, false, false };   // nezaberá podlahu ďaleko od steny

    // ───────────────────────── drobnosti pri ceste ─────────────────────────

    static void Cone(Transform r, Pal p, Vector3 pos)
    {
        Transform g = Group(r, pos, V0, 1.5f);
        Box(g, p.colors[4], V(0f, 0.04f, 0f), V(0.8f, 0.08f, 0.8f));
        Part(g, Lathe(10, 0.3f, 0.08f, 0.06f, 1f, 0f, 1f), p.colors[4]);
        Part(g, Lathe(10, 0.225f, 0.4f, 0.167f, 0.62f), p.white);
    }

    static void Sign(Transform r, Pal p, Random rnd, Vector3 pos)
    {
        Cyl(r, p.white, pos + V(0f, 1.4f, 0f), 0.14f, 2.8f);
        if (rnd.Next(2) == 0)
        {
            Cyl(r, p.colors[0], pos + V(0f, 3.1f, 0f), 1f, 0.1f, V(90f, 0f, 0f));
            Cyl(r, p.white, pos + V(0f, 3.1f, 0f), 0.7f, 0.14f, V(90f, 0f, 0f));
        }
        else Part(r, BoxMesh(V(0.85f, 0.85f, 0.1f), 2f), p.colors[1], pos + V(0f, 3.1f, 0f), V1, V(0f, 0f, 45f));
    }

    // domčeky, stromčeky, plot, lampy na 20 m od z0. Vráti true, ak je pri ceste plot.
    static bool TownDecor(Transform r, Pal p, Random rnd, int s, float z0)
    {
        bool fence = rnd.NextDouble() < 0.4;
        if (fence)
        {
            for (float z = 0.4f; z < 20f; z += 0.8f)
                Box(r, p.fence, V(s * 5.4f, 0.45f, z0 + z), V(0.12f, 0.9f, 0.12f));
            Box(r, p.fence, V(s * 5.4f, 0.3f, z0 + 10f), V(0.06f, 0.1f, 20f));
            Box(r, p.fence, V(s * 5.4f, 0.7f, z0 + 10f), V(0.06f, 0.1f, 20f));
        }

        if (rnd.NextDouble() < 0.45)
        {
            float w = R(rnd, 4f, 6f), h = R(rnd, 3f, 4.5f), d = R(rnd, 4f, 6f);
            float x = s * (9f + w / 2f + R(rnd, 0f, 2.5f));
            float z = z0 + R(rnd, d / 2f + 0.5f, 20f - d / 2f - 0.5f);
            float face = x - s * (w / 2f + 0.03f);
            Box(r, Pick(rnd, p.house), V(x, h / 2f, z), V(w, h, d), 1f);
            Roof(r, Pick(rnd, p.roof), V(x, h, z), w + 0.6f, h * 0.5f, d + 0.6f);
            Box(r, p.door, V(face, 0.8f, z), V(0.06f, 1.6f, 0.9f));
            foreach (float dz in new[] { -0.3f, 0.3f })
                Box(r, p.houseWindow, V(face, h * 0.62f, z + dz * d), V(0.06f, 0.8f, 0.8f));
            foreach (float dx in new[] { -0.25f, 0.25f })
                Box(r, p.houseWindow, V(x + dx * w, h * 0.6f, z - d / 2f - 0.03f), V(0.8f, 0.8f, 0.06f));
            Box(r, p.blocks[0], V(x + w * 0.2f, h * 1.35f, z + d * 0.2f), V(0.5f, 1.1f, 0.5f));
        }

        int trees = rnd.Next(1, 4);
        for (int i = 0; i < trees; i++)
        {
            float x = s * R(rnd, 6.3f, 8f), z = z0 + R(rnd, 1f, 19f), c = R(rnd, 1.6f, 2.3f);
            Cyl(r, p.trunk, V(x, 0.6f, z), 0.28f, 1.2f);
            Ball(r, Pick(rnd, p.leaves), V(x, 1.1f + c * 0.45f, z), c);
        }

        if (rnd.Next(2) == 0 || p.night)
        {
            Cyl(r, p.lampPost, V(s * 4.8f, 1.5f, z0 + 10f), 0.15f, 3f);
            Box(r, p.lampPost, V(s * 4.4f, 3f, z0 + 10f), V(0.8f, 0.1f, 0.1f));
            Ball(r, p.lampHead, V(s * 4.05f, 2.85f, z0 + 10f), 0.45f);
        }
        return fence;
    }

    // pieskovisko: kaktusy, hrad z piesku, vedierko, palma, pyramída
    static bool DesertDecor(Transform r, Pal p, Random rnd, int s, float z0)
    {
        for (int i = 0; i < 2; i++)
        {
            float x = s * R(rnd, 6.3f, 9f), z = z0 + R(rnd, 1f, 19f);
            Shape(r, "Capsule", p.cactus, V(x, 1.3f, z), V(0.7f, 1.3f, 0.7f));
            Shape(r, "Capsule", p.cactus, V(x + 0.55f, 1.6f, z), V(0.35f, 0.5f, 0.35f));
            Shape(r, "Capsule", p.cactus, V(x - 0.55f, 1.2f, z), V(0.35f, 0.45f, 0.35f));
        }

        float cx = s * R(rnd, 11.5f, 14f), cz = z0 + R(rnd, 5f, 15f);
        switch (rnd.Next(4))
        {
            case 0:   // hrad z piesku
                Box(r, p.castle, V(cx, 1f, cz), V(5f, 2f, 5f), 1f);
                foreach (float dx in new[] { -2.5f, 2.5f })
                    foreach (float dz in new[] { -2.5f, 2.5f })
                        Cyl(r, p.castle, V(cx + dx, 1.6f, cz + dz), 1.2f, 3.2f);
                Cyl(r, p.trunk, V(cx, 3f, cz), 0.08f, 2f);
                Box(r, p.blocks[0], V(cx + 0.4f, 3.7f, cz), V(0.8f, 0.5f, 0.05f));
                break;
            case 1:   // stupňovitá pyramída
                for (int i = 0; i < 4; i++)
                    Box(r, p.castle, V(cx, 0.6f + i * 1.2f, cz), V(7f - i * 1.6f, 1.2f, 7f - i * 1.6f), 1f);
                break;
            case 2:   // palma
                for (int i = 0; i < 5; i++)
                    Cyl(r, p.trunk, V(cx + s * i * 0.15f, 0.5f + i * 1f, cz), 0.5f - i * 0.04f, 1f);
                Vector3 top = V(cx + s * 0.75f, 5.2f, cz);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward;
                    Ball(r, p.leaves[0], top + dir * 1.4f + Vector3.down * 0.3f, V(0.9f, 0.25f, 3f), V(15f, i * 60f, 0f));
                }
                break;
            default:  // vedierko a lopatka
                Material c = Pick(rnd, p.colors);
                Part(r, Lathe(12, 0f, 0f, 0.5f, 0f, 0.7f, 1.4f, 0f, 1.4f), c, V(cx, 0f, cz), V1, V0);
                Box(r, c, V(cx + 1.5f, 0.06f, cz + 0.5f), V(0.12f, 0.12f, 1.6f), 1f, 25f);
                Box(r, c, V(cx + 1.85f, 0.04f, cz + 1.3f), V(0.6f, 0.06f, 0.7f), 1f, 25f);
                break;
        }

        bool fence = rnd.NextDouble() < 0.3;
        if (fence)
        {
            for (float z = 1f; z < 20f; z += 2f)
                Box(r, p.fence, V(s * 5.4f, 0.4f, z0 + z), V(0.15f, 0.8f, 0.15f));
            Box(r, p.fence, V(s * 5.4f, 0.6f, z0 + 10f), V(0.08f, 0.12f, 20f));
        }
        return fence;
    }

    static void Roadside(Transform r, Pal p, Random rnd, int s, float z0)
    {
        bool fence = p.desert ? DesertDecor(r, p, rnd, s, z0) : TownDecor(r, p, rnd, s, z0);

        // hračky hneď pri obrubníku; pri plote je miesto len na značku
        switch (rnd.Next(fence ? 2 : 6))
        {
            case 0: Sign(r, p, rnd, V(s * 4.9f, 0f, z0 + 4f)); break;
            case 2:
                for (int k = 0; k < 4; k++) Cone(r, p, V(s * 5.1f, 0f, z0 + 3f + k * 4.5f));
                break;
            case 3:   // domino
                for (int k = 0; k < 9; k++)
                    Box(r, p.colors[k % p.colors.Length], V(s * 5.1f, 0.8f, z0 + 3f + k * 1.3f), V(0.9f, 1.6f, 0.25f));
                break;
            case 4:
                for (int k = 0; k < 5; k++)
                {
                    float b = R(rnd, 0.9f, 1.4f);
                    Box(r, Pick(rnd, p.blocks), V(s * R(rnd, 5f, 6f), b / 2f, z0 + 2f + k * 3.6f), V1 * b, b, R(rnd, 0f, 90f));
                }
                break;
            case 5:   // guľôčky
                for (int k = 0; k < 4; k++)
                {
                    float d = R(rnd, 0.6f, 1f);
                    Ball(r, Pick(rnd, p.colors), V(s * R(rnd, 4.8f, 6f), d / 2f, z0 + R(rnd, 2f, 18f)), d);
                }
                break;
        }
    }

    // brána z kociek ponad cestu
    static void Arch(Transform r, Pal p, Random rnd)
    {
        const float z = SegLen / 2f, b = 2.2f;
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < 4; i++)
                Box(r, Pick(rnd, p.blocks), V(s * 6.4f, b / 2f + i * b, z), V1 * b, b, R(rnd, -6f, 6f));
        Box(r, p.blocks[1], V(0f, 4f * b + 0.6f, z), V(15f, 1.2f, b), b);
        Part(r, Prism(b, -3f, 0f, 3f, 0f, 0f, 2f), p.blocks[0], V(0f, 4f * b + 1.2f, z), V1, V0);
    }

    // ───────────────────────── úseky cesty ─────────────────────────

    // cesta, obrubník, koberec, parkety, steny izby
    static void BuildRoom(Transform r, Pal p)
    {
        // cesta: textúra 8 m × 10 m, u = (x + 4) / 8, v = z / 10
        Mesh road = new Mesh();
        road.vertices = new[] { new Vector3(-4f, 0f, 0f), new Vector3(-4f, 0f, SegLen), new Vector3(4f, 0f, SegLen), new Vector3(4f, 0f, 0f) };
        road.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, SegLen / 10f), new Vector2(1f, SegLen / 10f), new Vector2(1f, 0f) };
        road.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        road.RecalculateNormals();
        road.RecalculateBounds();
        Part(r, road, p.road);

        float mid = SegLen / 2f;
        for (int s = -1; s <= 1; s += 2)
        {
            Box(r, p.curb, V(s * 4.1f, 0.04f, mid), V(0.2f, 0.08f, SegLen));
            Box(r, p.rug, V(s * 11.1f, -0.03f, mid), V(13.8f, 0.06f, SegLen), 2f);            // hrací koberec s cestou
            Box(r, p.rugEdge, V(s * 18.3f, -0.02f, mid), V(0.6f, 0.08f, SegLen), 2f);
            Box(r, p.wall, V(s * (WallX + 0.5f), WallH / 2f, mid), V(1f, WallH, SegLen), SegLen);   // dlaždica tapety = dĺžka úseku, takže nadväzuje
            Box(r, p.white, V(s * (WallX - 0.2f), 1f, mid), V(0.4f, 2f, SegLen));
        }
        Box(r, p.floor, V(0f, -0.06f, mid), V(WallX * 2f, 0.08f, SegLen), 4f);
    }

    static GameObject BuildSegment(Pal p, int i)
    {
        GameObject root = new GameObject("RoadSegment_" + p.name + "_" + i);
        Transform r = root.transform;
        Random rnd = new Random(p.name[0] * 131 + i * 7919);
        BuildRoom(r, p);

        List<int> order = Shuffled(rnd, Toys.Length);
        int next = 0;
        for (int s = -1; s <= 1; s += 2)
        {
            // každý variant má na ľavej a pravej stene inú vec
            Transform wall = Group(r, V(s * WallX, 0f, SegLen / 2f), V(0f, s > 0 ? 180f : 0f, 0f), 1f);
            int feature = (i + (s > 0 ? 3 : 0)) % WallFeatures.Length;
            WallFeatures[feature](wall, p, rnd);

            // políčka 10 × 10 m medzi kobercom a stenou: bližší pás vždy, vzdialenejší len keď pri stene nestojí nič hlboké
            List<int> cells = Shuffled(rnd, 4);
            for (int k = 0; k < 3; k++)
                Toys[order[next++ % order.Count]](r, p, rnd, V(s * R(rnd, 23f, 26f), 0f, cells[k] * 10f + 5f + R(rnd, -2f, 2f)));
            if (Shallow[feature])
                for (int k = 0; k < 2; k++)
                    Toys[order[next++ % order.Count]](r, p, rnd, V(s * R(rnd, 35f, 37f), 0f, cells[k + 2] * 10f + 5f + R(rnd, -2f, 2f)));

            for (int half = 0; half < 2; half++) Roadside(r, p, rnd, s, half * 20f);

            if (p.night)
                for (int k = 0; k < 10; k++)
                {
                    Box(r, p.star, V(s * R(rnd, 6f, 17f), 0.01f, R(rnd, 1f, SegLen - 1f)), V(0.6f, 0.02f, 0.6f), 1f, 45f);
                    Box(r, p.star, V(s * (WallX - 0.05f), R(rnd, 6f, 55f), R(rnd, 1f, SegLen - 1f)), V(0.1f, 1.2f, 1.2f), 1f);
                }
        }
        if (i == 2) Arch(r, p, rnd);

        Combine(root, "Seg_" + p.name + "_" + i);
        string path = PrefabDir + "/" + root.name + ".prefab";
        made.Add(path);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildTheme(Pal p)
    {
        GameObject[] segs = new GameObject[Variants];
        for (int i = 0; i < Variants; i++) segs[i] = BuildSegment(p, i);

        GameObject env = new GameObject("Environment_" + p.name);
        EnvironmentTheme t = env.AddComponent<EnvironmentTheme>();
        RoadSpawner sp = env.AddComponent<RoadSpawner>();
        sp.segments = segs;
        sp.segmentLength = SegLen;
        sp.ahead = 6;
        sp.behind = 2;

        // namiesto oblohy panoráma izby s tou istou tapetou; hmla má farbu tapety, takže diaľka splynie so stenou
        Material sky = new Material(Shader.Find("Skybox/Panoramic"));
        sky.SetTexture("_MainTex", Panorama("Izba_" + p.name, p.bg, p.cloud, p.shadow, p.ceiling, p.night));
        sky.SetFloat("_Mapping", 1f);
        sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
        sky.SetFloat("_Exposure", 1f);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));   // 0.5 = bez zmeny farby
        t.skybox = Save(sky, Art + "/Materials/" + p.name + "_Obloha.mat");
        t.fogColor = p.bg;
        t.fogStart = 70f;
        t.fogEnd = 200f;
        if (p.night)
        {
            t.fogStart = 30f; t.fogEnd = 160f;
            t.ambientSky = new Color(0.32f, 0.36f, 0.6f); t.ambientEquator = new Color(0.22f, 0.24f, 0.42f); t.ambientGround = new Color(0.1f, 0.1f, 0.18f);
            t.sunColor = new Color(0.6f, 0.7f, 1f); t.sunIntensity = 0.6f; t.sunRotation = new Vector3(40f, -20f, 0f);
        }
        else if (p.desert)
        {
            t.ambientSky = new Color(0.95f, 0.85f, 0.72f); t.ambientEquator = new Color(0.8f, 0.7f, 0.58f); t.ambientGround = new Color(0.55f, 0.45f, 0.35f);
            t.sunColor = new Color(1f, 0.88f, 0.7f); t.sunIntensity = 1.3f; t.sunRotation = new Vector3(40f, 35f, 0f);
        }
        else
        {
            t.ambientSky = new Color(0.82f, 0.86f, 0.95f); t.ambientEquator = new Color(0.72f, 0.7f, 0.66f); t.ambientGround = new Color(0.5f, 0.42f, 0.34f);
            t.sunColor = new Color(1f, 0.96f, 0.88f); t.sunIntensity = 1.2f; t.sunRotation = new Vector3(50f, -35f, 0f);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(env, PrefabDir + "/Environment_" + p.name + ".prefab");
        Object.DestroyImmediate(env);
        return prefab;
    }

    // ───────────────────────── štart / cieľ ─────────────────────────

    static GameObject BuildLine(string name, bool gantry)
    {
        Material checker = Mat("Sachovnica", Color.white, texChecker);
        Material red = Mat("Kocka_Cervena", ToyColors[0], texBlock);
        Material yellow = Mat("Kocka_Zlta", ToyColors[1], texBlock);

        GameObject root = new GameObject(name);
        Box(root.transform, checker, new Vector3(0f, 0.012f, 0f), new Vector3(8f, 0.02f, 1.5f), 1f);
        if (gantry)
        {
            // brána z kociek: červeno-žlté stĺpy, šachovnicový pás
            foreach (float x in new[] { -5.2f, 5.2f })
                for (int i = 0; i < 5; i++)
                    Box(root.transform, i % 2 == 0 ? red : yellow, new Vector3(x, 0.5f + i, 0f), Vector3.one, 1f);
            Box(root.transform, checker, new Vector3(0f, 5.4f, 0f), new Vector3(11.4f, 0.8f, 0.4f), 0.8f);
        }
        Combine(root, name);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildLoader(GameObject[] themes)
    {
        GameObject go = new GameObject("Prostredie");
        go.AddComponent<ThemeLoader>().themes = themes;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Prostredie.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ───────────────────────── autíčka ─────────────────────────
    // Baculaté hračkárske autíčka: krátke, vysoké, veľké kolesá. Koreň prefabu je v strede collidera
    // (1.6 × 1 × 3.5, y = 0.5 nad cestou), takže zem je y = -0.5 a auto sa musí zmestiť do ±0.8 × ±1.7.

    class CarParts
    {
        public Material glass, tyre, hub, dark, head, tail, white, red, yellow, blueLight, checker, smoke, flame;
        public List<KeyValuePair<Mesh, Material>> wheel = new List<KeyValuePair<Mesh, Material>>();
    }

    // zrezaný ihlan: obdĺžnik dole (šírka wB, z od zB0 po zB1, výška y0) a obdĺžnik hore
    struct Fr
    {
        public float wB, zB0, zB1, y0, wT, zT0, zT1, y1;

        public Fr(float wB, float zB0, float zB1, float y0, float wT, float zT0, float zT1, float y1)
        {
            this.wB = wB; this.zB0 = zB0; this.zB1 = zB1; this.y0 = y0;
            this.wT = wT; this.zT0 = zT0; this.zT1 = zT1; this.y1 = y1;
        }

        // pás medzi výškami t0..t1 (0 = spodok, 1 = vrch) zväčšený o g: okná okolo kabíny
        public Fr Band(float t0, float t1, float g)
        {
            return new Fr(Mathf.Lerp(wB, wT, t0) + 2f * g, Mathf.Lerp(zB0, zT0, t0) - g, Mathf.Lerp(zB1, zT1, t0) + g, Mathf.Lerp(y0, y1, t0),
                          Mathf.Lerp(wB, wT, t1) + 2f * g, Mathf.Lerp(zB0, zT0, t1) - g, Mathf.Lerp(zB1, zT1, t1) + g, Mathf.Lerp(y0, y1, t1));
        }

        public Mesh Mesh()
        {
            Vector3 a = V(-wB / 2f, y0, zB0), b = V(wB / 2f, y0, zB0), c = V(wB / 2f, y0, zB1), d = V(-wB / 2f, y0, zB1);
            Vector3 e = V(-wT / 2f, y1, zT0), f = V(wT / 2f, y1, zT0), g = V(wT / 2f, y1, zT1), h = V(-wT / 2f, y1, zT1);
            return Poly(new List<Vector3[]>
            {
                new[] { a, b, c, d }, new[] { e, f, g, h }, new[] { a, b, f, e },
                new[] { b, c, g, f }, new[] { c, d, h, g }, new[] { d, a, e, h },
            }, 1f);
        }
    }

    static void Hull(Transform b, Material m, Fr f) { Part(b, f.Mesh(), m); }

    static void Cabin(Transform b, CarParts m, Material paint, Fr f, float t0 = 0.16f, float t1 = 0.82f)
    {
        Hull(b, paint, f);
        Hull(b, m.glass, f.Band(t0, t1, 0.012f));
    }

    // spodok karosérie po výšku top + podvozok, nárazníky, svetlá a čierne blatníky nad kolesami
    static void Lower(Transform b, CarParts m, Material paint, float top)
    {
        Hull(b, paint, new Fr(1.36f, -1.56f, 1.56f, -0.05f, 1.44f, -1.62f, 1.62f, 0.12f));
        Hull(b, paint, new Fr(1.44f, -1.62f, 1.62f, 0.12f, 1.4f, -1.6f, 1.56f, top));
        Box(b, m.dark, V(0f, -0.18f, 0f), V(1f, 0.28f, 3f));
        for (int s = -1; s <= 1; s += 2)
        {
            Box(b, m.dark, V(0f, -0.1f, s * 1.66f), V(1.5f, 0.2f, 0.14f));
            Box(b, m.head, V(s * 0.5f, top - 0.13f, 1.6f), V(0.3f, 0.14f, 0.06f));
            Box(b, m.tail, V(s * 0.52f, top - 0.13f, -1.6f), V(0.28f, 0.14f, 0.06f));
            for (int z = -1; z <= 1; z += 2)
                Box(b, m.dark, V(s * 0.73f, 0.38f, z * 1.02f), V(0.2f, 0.07f, 1f));
        }
    }

    static void Wheel(Transform parent, CarParts m, Vector3 pos, Vector3 euler, float scale)
    {
        Transform g = Group(parent, pos, euler, scale);
        foreach (KeyValuePair<Mesh, Material> w in m.wheel) Part(g, w.Key, w.Value);
    }

    // svetelná rampa na streche: lišta a svetlá daných farieb
    static void LightBar(Transform b, CarParts m, float y, float z, params Material[] lights)
    {
        Box(b, m.dark, V(0f, y, z), V(1f, 0.05f, 0.16f));
        float w = 0.9f / lights.Length;
        for (int i = 0; i < lights.Length; i++)
            Box(b, lights[i], V((i - (lights.Length - 1) / 2f) * w, y + 0.07f, z), V(w - 0.04f, 0.1f, 0.14f));
    }

    // džíp: hranatá kabína, strešný nosič, rezerva vzadu (hráč má biele pretekárske pruhy)
    static void Jeep(Transform b, CarParts m, Material paint, bool stripes)
    {
        Lower(b, m, paint, 0.42f);
        Cabin(b, m, paint, new Fr(1.36f, -1.56f, 0.62f, 0.42f, 1.22f, -1.48f, 0.32f, 1.02f));
        Box(b, paint, V(0f, 0.72f, -0.5f), V(1.34f, 0.44f, 0.1f));
        Box(b, m.dark, V(0f, 0.25f, 1.6f), V(0.7f, 0.2f, 0.06f));
        for (int s = -1; s <= 1; s += 2) Box(b, m.dark, V(s * 0.5f, 1.06f, -0.58f), V(0.05f, 0.05f, 1.5f));
        foreach (float z in new[] { -1.2f, -0.58f, 0.05f }) Box(b, m.dark, V(0f, 1.06f, z), V(1.05f, 0.04f, 0.05f));
        Wheel(b, m, V(0f, 0.42f, -1.74f), V(0f, 90f, 0f), 0.85f);
        if (stripes)
        {
            Box(b, m.white, V(0f, 0.425f, 1.09f), V(0.34f, 0.02f, 0.92f));
            Box(b, m.white, V(0f, 1.025f, -0.58f), V(0.34f, 0.02f, 1.7f));
        }
    }

    // otvorený športový džíp s ochranným rámom
    static void Sport(Transform b, CarParts m, Material paint)
    {
        Lower(b, m, paint, 0.42f);
        Hull(b, m.glass, new Fr(1.3f, 0.36f, 0.5f, 0.42f, 1.2f, 0.16f, 0.24f, 0.92f));
        Box(b, paint, V(0f, 0.93f, 0.2f), V(1.26f, 0.05f, 0.12f));
        for (int s = -1; s <= 1; s += 2)
        {
            Box(b, m.dark, V(s * 0.3f, 0.6f, -0.2f), V(0.42f, 0.36f, 0.42f));       // sedadlo
            Box(b, m.dark, V(s * 0.3f, 0.78f, -0.45f), V(0.42f, 0.6f, 0.12f));
            Box(b, m.dark, V(s * 0.6f, 0.72f, -0.65f), V(0.08f, 0.6f, 0.08f));      // rám
            Box(b, m.dark, V(s * 0.727f, 0.22f, 0f), V(0.02f, 0.12f, 2.2f));        // pásik na boku
        }
        Box(b, m.dark, V(0f, 1.02f, -0.65f), V(1.28f, 0.08f, 0.08f));
        foreach (float x in new[] { -0.42f, -0.14f, 0.14f, 0.42f }) Box(b, m.head, V(x, 1.11f, -0.63f), V(0.2f, 0.1f, 0.06f));
        Box(b, paint, V(0f, 0.5f, -1.2f), V(1.38f, 0.16f, 0.76f));
        Box(b, m.dark, V(0f, 0.25f, 1.6f), V(0.7f, 0.2f, 0.06f));
        Wheel(b, m, V(0f, 0.5f, -1.74f), V(0f, 90f, 0f), 0.85f);
    }

    // pickup s kockami na korbe
    static void Pickup(Transform b, CarParts m, Material paint)
    {
        Lower(b, m, paint, 0.4f);
        Cabin(b, m, paint, new Fr(1.36f, -0.38f, 0.62f, 0.4f, 1.22f, -0.32f, 0.32f, 1f));
        for (int s = -1; s <= 1; s += 2) Box(b, paint, V(s * 0.66f, 0.53f, -0.98f), V(0.08f, 0.26f, 1.22f));
        Box(b, paint, V(0f, 0.53f, -1.57f), V(1.4f, 0.26f, 0.08f));
        Box(b, m.dark, V(0f, 0.41f, -0.98f), V(1.24f, 0.02f, 1.16f));
        Box(b, m.yellow, V(-0.25f, 0.62f, -0.8f), V1 * 0.4f, 2f, 15f);
        Box(b, m.red, V(0.26f, 0.6f, -1.2f), V1 * 0.36f, 2f, -20f);
        LightBar(b, m, 1.03f, 0f, m.head, m.head, m.head, m.head);
        Box(b, m.dark, V(0f, 0.24f, 1.6f), V(0.7f, 0.2f, 0.06f));
    }

    // malý hatchback so spojlerom
    static void Hatch(Transform b, CarParts m, Material paint)
    {
        Lower(b, m, paint, 0.36f);
        Cabin(b, m, paint, new Fr(1.38f, -1.58f, 0.8f, 0.36f, 1.2f, -1.2f, 0.25f, 0.98f));
        Box(b, paint, V(0f, 0.67f, -0.35f), V(1.34f, 0.42f, 0.1f));
        Box(b, paint, V(0f, 1f, -1.22f), V(1.2f, 0.05f, 0.3f));
        for (int s = -1; s <= 1; s += 2) Box(b, m.white, V(s * 0.3f, 0.365f, 1.17f), V(0.14f, 0.02f, 0.76f));
        Box(b, m.dark, V(0f, 0.2f, 1.6f), V(0.6f, 0.14f, 0.06f));
    }

    static void Sedan(Transform b, CarParts m, Material paint)
    {
        Lower(b, m, paint, 0.34f);
        Cabin(b, m, paint, new Fr(1.36f, -0.98f, 0.72f, 0.34f, 1.18f, -0.66f, 0.3f, 0.86f));
        Box(b, paint, V(0f, 0.6f, -0.15f), V(1.32f, 0.36f, 0.09f));
        Box(b, m.dark, V(0f, 0.19f, 1.6f), V(0.6f, 0.14f, 0.06f));
    }

    static void Taxi(Transform b, CarParts m, Material paint)
    {
        Sedan(b, m, paint);
        for (int s = -1; s <= 1; s += 2) Box(b, m.checker, V(s * 0.727f, 0.18f, 0f), V(0.02f, 0.14f, 2.8f), 0.28f);
        Box(b, m.yellow, V(0f, 0.94f, -0.18f), V(0.5f, 0.16f, 0.2f));
    }

    static void Police(Transform b, CarParts m, Material paint)
    {
        Sedan(b, m, paint);
        for (int s = -1; s <= 1; s += 2) Box(b, m.white, V(s * 0.727f, 0.15f, -0.13f), V(0.02f, 0.34f, 1.3f));
        Box(b, m.white, V(0f, 0.865f, -0.18f), V(1.12f, 0.02f, 0.9f));
        LightBar(b, m, 0.9f, -0.18f, m.tail, m.blueLight);
    }

    static void Bus(Transform b, CarParts m, Material paint)
    {
        Lower(b, m, paint, 0.3f);
        Fr up = new Fr(1.44f, -1.62f, 1.6f, 0.3f, 1.4f, -1.6f, 1.5f, 1.22f);
        Hull(b, paint, up);
        Hull(b, m.glass, up.Band(0.45f, 0.82f, 0.012f));
        for (float z = -1.25f; z <= 1f; z += 0.45f) Box(b, paint, V(0f, 0.885f, z), V(1.47f, 0.36f, 0.09f));
        Hull(b, paint, new Fr(1.4f, -1.6f, 1.5f, 1.22f, 1.2f, -1.5f, 1.36f, 1.3f));
        for (int s = -1; s <= 1; s += 2)
            foreach (float y in new[] { 0.42f, 0.56f })
                Box(b, m.dark, V(s * 0.728f, y, 0f), V(0.02f, 0.05f, 3.1f));
        Box(b, m.red, V(-0.745f, 0.8f, 0.55f), V(0.03f, 0.22f, 0.22f));
        Box(b, m.dark, V(0f, 0.5f, 1.6f), V(0.9f, 0.12f, 0.06f));
    }

    static void Ambulance(Transform b, CarParts m, Material paint)
    {
        Lower(b, m, paint, 0.36f);
        Hull(b, paint, new Fr(1.42f, -1.62f, 0.3f, 0.36f, 1.38f, -1.6f, 0.3f, 1.14f));
        Cabin(b, m, paint, new Fr(1.42f, 0.3f, 1.5f, 0.36f, 1.38f, 0.3f, 0.98f, 1.14f), 0.3f, 0.8f);
        for (int s = -1; s <= 1; s += 2)
        {
            Box(b, m.red, V(s * 0.722f, 0.52f, -0.65f), V(0.02f, 0.1f, 1.9f));
            Box(b, m.red, V(s * 0.715f, 0.86f, -0.65f), V(0.02f, 0.3f, 0.1f));
            Box(b, m.red, V(s * 0.715f, 0.86f, -0.65f), V(0.02f, 0.1f, 0.3f));
            Box(b, m.glass, V(s * 0.32f, 0.85f, -1.615f), V(0.42f, 0.3f, 0.03f));
        }
        LightBar(b, m, 1.17f, 0.5f, m.blueLight, m.blueLight);
    }

    delegate void Style(Transform b, CarParts m);

    static void BuildCars()
    {
        CarParts m = new CarParts
        {
            glass = Mat("Auto_Sklo", new Color(0.13f, 0.19f, 0.3f), null, Plastic),
            tyre = Mat("Auto_Pneumatika", new Color(0.1f, 0.1f, 0.11f)),
            hub = Mat("Auto_Disk", new Color(0.85f, 0.85f, 0.88f), null, Plastic),
            dark = Mat("Auto_Naraznik", new Color(0.17f, 0.17f, 0.19f)),
            head = Mat("Auto_Svetlo", new Color(1f, 0.95f, 0.75f), null, Glow),
            tail = Mat("Auto_Koncove", new Color(1f, 0.15f, 0.1f), null, Glow),
            blueLight = Mat("Auto_Majak", new Color(0.2f, 0.5f, 1f), null, Glow),
            white = Mat("Auto_Biela", new Color(0.97f, 0.97f, 0.97f), null, Plastic),
            red = Mat("Auto_Cervena", new Color(0.88f, 0.14f, 0.12f), null, Plastic),
            yellow = Mat("Auto_Zlta", new Color(1f, 0.82f, 0.15f), null, Plastic),
            checker = Mat("Sachovnica", Color.white, texChecker),
            smoke = ParticleMat("Fx_Dym", false),
            flame = ParticleMat("Fx_Nitro", true),
        };
        Material orange = Mat("Auto_Oranzova", new Color(1f, 0.55f, 0.1f), null, Plastic);
        Material blue = Mat("Auto_Modra", new Color(0.16f, 0.4f, 0.88f), null, Plastic);
        Material green = Mat("Auto_Zelena", new Color(0.2f, 0.62f, 0.3f), null, Plastic);
        Material taxi = Mat("Auto_Taxi", new Color(0.45f, 0.76f, 0.93f), null, Plastic);
        Material bus = Mat("Auto_Autobus", new Color(1f, 0.7f, 0.1f), null, Plastic);
        Material black = Mat("Auto_Cierna", new Color(0.1f, 0.1f, 0.12f), null, Plastic);

        // koleso: pneumatika, disk a päť dier, aby bolo vidno točenie; os X
        GameObject w = new GameObject("Koleso");
        Part(w.transform, Lathe(16, 0f, -0.15f, 0.33f, -0.16f, 0.4f, -0.13f, 0.42f, -0.06f, 0.42f, 0.06f, 0.4f, 0.13f, 0.33f, 0.16f, 0f, 0.15f),
             m.tyre, V0, V1, V(0f, 0f, 90f));
        Part(w.transform, Lathe(12, 0f, -0.17f, 0.24f, -0.17f, 0.24f, -0.17f, 0.24f, 0.17f, 0.24f, 0.17f, 0f, 0.17f), m.hub, V0, V1, V(0f, 0f, 90f));
        for (int k = 0; k < 5; k++)
        {
            float a = k * 72f * Mathf.Deg2Rad;
            Box(w.transform, m.dark, V(0f, Mathf.Sin(a) * 0.14f, Mathf.Cos(a) * 0.14f), V(0.35f, 0.07f, 0.07f));
        }
        Combine(w, "Koleso");
        foreach (MeshFilter mf in w.GetComponentsInChildren<MeshFilter>())
            m.wheel.Add(new KeyValuePair<Mesh, Material>(mf.sharedMesh, mf.GetComponent<MeshRenderer>().sharedMaterial));
        Object.DestroyImmediate(w);

        BuildCar("Assets/Prefabs/Car.prefab", m, CarKind.Player, (b, p) => Jeep(b, p, p.red, true));
        BuildCar("Assets/Prefabs/EnemyCar (1).prefab", m, CarKind.Enemy, (b, p) => Sport(b, p, orange));
        BuildCar("Assets/Prefabs/EnemyCar (2).prefab", m, CarKind.Enemy, (b, p) => Pickup(b, p, blue));
        BuildCar("Assets/Prefabs/EnemyCar (3).prefab", m, CarKind.Enemy, (b, p) => Hatch(b, p, green));
        BuildCar("Assets/Prefabs/TrafficCar.prefab", m, CarKind.Traffic,
                 (b, p) => Taxi(b, p, taxi), (b, p) => Bus(b, p, bus), (b, p) => Police(b, p, black), (b, p) => Ambulance(b, p, p.white));
    }

    enum CarKind { Player, Enemy, Traffic }

    // Mení iba potomka "Model": koreň, jeho Box Collider, Rigidbody a skripty ostávajú.
    // Viac štýlov = viac karosérií v jednom prefabe, CarEffects pri štarte jednu náhodne nechá.
    static void BuildCar(string path, CarParts m, CarKind kind, params Style[] styles)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("Samo: chýba prefab " + path); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform model = root.transform.Find("Model");
        if (model == null)
        {
            Debug.LogError("Samo: prefab " + path + " nemá potomka Model, preskakujem.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        foreach (Component c in model.GetComponents<Component>())
            if (!(c is Transform)) Object.DestroyImmediate(c);
        for (int i = model.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(model.GetChild(i).gameObject);
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;

        string id = "Auto_" + Path.GetFileNameWithoutExtension(path).Replace(" ", "").Replace("(", "").Replace(")", "");
        GameObject[] bodies = new GameObject[styles.Length];
        for (int i = 0; i < styles.Length; i++)
        {
            GameObject b = new GameObject("Karoseria" + i);
            b.transform.SetParent(model, false);
            styles[i](b.transform, m);
            Combine(b, id + "_" + i);
            bodies[i] = b;
        }

        List<Transform> wheels = new List<Transform>();
        foreach (float x in new[] { -0.62f, 0.62f })
            foreach (float z in new[] { -1.02f, 1.02f })
            {
                Transform pivot = new GameObject("Koleso").transform;
                pivot.SetParent(model, false);
                pivot.localPosition = new Vector3(x, -0.08f, z);
                foreach (KeyValuePair<Mesh, Material> part in m.wheel) Part(pivot, part.Key, part.Value);
                wheels.Add(pivot);
            }

        CarEffects fx = model.gameObject.AddComponent<CarEffects>();
        fx.wheels = wheels.ToArray();
        fx.wheelRadius = 0.42f;
        fx.models = styles.Length > 1 ? bodies : new GameObject[0];
        fx.smoke = Fx(model, "Dym", new Vector3(0.4f, -0.3f, -1.75f), m.smoke, 0.9f, 0.6f, 0.25f, new Color(0.85f, 0.85f, 0.85f, 0.45f), 6f, 3f, true);
        if (kind == CarKind.Player)
            fx.nitro = Fx(model, "Nitro", new Vector3(0f, -0.2f, -1.78f), m.flame, 0.18f, 7f, 0.45f, new Color(1f, 0.55f, 0.15f, 0.9f), 70f, 0.2f, false);
        if (kind != CarKind.Traffic)
        {
            fx.dust = new[]
            {
                Fx(model, "Prach_L", new Vector3(-0.7f, -0.45f, -1.2f), m.smoke, 0.8f, 1.2f, 0.35f, new Color(0.9f, 0.85f, 0.75f, 0.3f), 10f, 3.5f, true),
                Fx(model, "Prach_P", new Vector3(0.7f, -0.45f, -1.2f), m.smoke, 0.8f, 1.2f, 0.35f, new Color(0.9f, 0.85f, 0.75f, 0.3f), 10f, 3.5f, true),
            };
        }
        else fx.dust = new ParticleSystem[0];

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static ParticleSystem Fx(Transform parent, string name, Vector3 pos, Material mat, float life, float speed, float size,
                             Color color, float rate, float endSize, bool on)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // strieľa dozadu

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = life;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.gravityModifier = -0.02f;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        em.enabled = on;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 12f;
        sh.radius = 0.05f;

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mat;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    // ───────────────────────── testovacia scéna ─────────────────────────

    static void BuildTestScene(GameObject loader, GameObject start, GameObject finish)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car.prefab"));
        car.transform.position = new Vector3(1f, 0.5f, 0f);

        string[] enemies = { "EnemyCar (1)", "EnemyCar (2)", "EnemyCar (3)" };
        Vector3[] pos = { new Vector3(-3f, 0.5f, 6f), new Vector3(-1f, 0.5f, 10f), new Vector3(3f, 0.5f, 4f) };
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + enemies[i] + ".prefab");
            if (prefab == null) continue;
            GameObject e = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            e.transform.position = pos[i];
            EnemyCar ec = e.GetComponent<EnemyCar>();
            if (ec != null) ec.player = car.transform;
        }

        Camera cam = Camera.main;
        cam.transform.position = car.transform.position + new Vector3(0f, 4f, -8f);
        cam.transform.LookAt(car.transform.position + Vector3.forward * 5f);
        cam.farClipPlane = 400f;
        cam.gameObject.AddComponent<CameraFollow>().target = car.transform;

        GameObject env = (GameObject)PrefabUtility.InstantiatePrefab(loader);
        env.GetComponent<ThemeLoader>().testTheme = 0;
        ((GameObject)PrefabUtility.InstantiatePrefab(start)).transform.position = Vector3.zero;
        ((GameObject)PrefabUtility.InstantiatePrefab(finish)).transform.position = new Vector3(0f, 0f, 150f);

        EditorSceneManager.SaveScene(scene, TestScene);
    }

    // ───────────────────────── screenshoty (batchmode) ─────────────────────────

    // Unity.exe -batchmode -projectPath . -executeMethod SamoSetup.Screenshots -quit  (výstup do SAMO_SHOTS alebo Temp/SamoShots)
    // Spúšťaj až po GenerateAll v samostatnom behu: hneď po generovaní má renderer ešte staré dáta materiálov.
    public static void Screenshots()
    {
        try
        {
            string dir = Environment.GetEnvironmentVariable("SAMO_SHOTS");
            if (string.IsNullOrEmpty(dir)) dir = "Temp/SamoShots";
            Directory.CreateDirectory(dir);

            string[] themes = { "Mesto", "Pust", "Noc" };
            foreach (string t in themes)
            {
                EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
                foreach (ThemeLoader l in Object.FindObjectsByType<ThemeLoader>(FindObjectsSortMode.None)) Object.DestroyImmediate(l.gameObject);

                GameObject env = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Environment_" + t + ".prefab"));
                env.GetComponent<EnvironmentTheme>().Apply();
                RoadSpawner sp = env.GetComponent<RoadSpawner>();
                for (int i = -1; i < 6; i++)
                {
                    bool turned = i % 2 != 0;   // ako RoadSpawner: každý druhý úsek otočený
                    Object.Instantiate(sp.segments[(i + 1) % sp.segments.Length], new Vector3(0f, 0f, (turned ? i + 1 : i) * SegLen), Quaternion.Euler(0f, turned ? 180f : 0f, 0f));
                }

                Camera cam = Camera.main;
                Transform car = Object.FindAnyObjectByType<CarController>().transform;
                cam.transform.position = car.position + new Vector3(0f, 4f, -8f);
                cam.transform.LookAt(car.position + Vector3.forward * 5f);
                Shot(cam, 1280, 720, dir + "/" + t + "_landscape.png");
                Shot(cam, 720, 1280, dir + "/" + t + "_portrait.png");

                // pohľad zvrchu na celú izbu
                cam.transform.position = car.position + new Vector3(0f, 22f, -30f);
                cam.transform.LookAt(car.position + new Vector3(0f, 8f, 40f));
                Shot(cam, 1280, 720, dir + "/" + t + "_izba.png");

                if (t != "Mesto") continue;

                // všetky autá: hráč a súperi sú v scéne, premávka po jednom type vedľa seba
                GameObject traffic = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrafficCar.prefab");
                for (int i = 0; i < 4; i++)
                {
                    GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(traffic);
                    go.transform.position = new Vector3(-3f + i * 2f, 0.5f, 16f);
                    GameObject[] models = go.GetComponentInChildren<CarEffects>().models;
                    for (int k = 0; k < models.Length; k++) models[k].SetActive(k == i);
                }
                cam.transform.position = new Vector3(7f, 4f, 22f);
                cam.transform.LookAt(new Vector3(0f, 0.3f, 9f));
                Shot(cam, 1280, 720, dir + "/auta_predok.png");
                cam.transform.position = new Vector3(-7f, 3.5f, -4f);
                cam.transform.LookAt(new Vector3(0f, 0.3f, 9f));
                Shot(cam, 1280, 720, dir + "/auta_zadok.png");
                cam.transform.position = new Vector3(4.5f, 1.6f, 2.5f);
                cam.transform.LookAt(car.position);
                Shot(cam, 1280, 720, dir + "/auto_hrac.png");
            }
            Debug.Log("Samo: screenshoty v " + Path.GetFullPath(dir));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    static void Shot(Camera cam, int w, int h, string file)
    {
        RenderTexture rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();   // prvý render po štarte editora ešte nemá skompilované shadery
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(tex);
        rt.Release();
    }
}
