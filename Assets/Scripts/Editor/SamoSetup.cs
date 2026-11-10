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
    const float SegLen = 20f;
    const int Variants = 4;

    static Texture2D texRoad, texFuzz, texWood, texWall, texChecker, texSoft;

    // ───────────────────────── menu ─────────────────────────

    [MenuItem("Tools/Samo/1. Vygeneruj prostredie, autá a Test_Samo")]
    public static void GenerateAll()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string d in new[] { Art + "/Textures", Art + "/Materials", Art + "/Meshes", PrefabDir })
            Directory.CreateDirectory(d);

        BuildTextures();
        GameObject[] themes =
        {
            BuildTheme("Mesto", CityPalette(), false),
            BuildTheme("Pust", DesertPalette(), true),
            BuildTheme("Noc", NightPalette(), false),
        };
        GameObject loader = BuildLoader(themes);
        GameObject start = BuildLine("StartLine", false);
        GameObject finish = BuildLine("FinishLine", true);
        BuildCars();
        BuildTestScene(loader, start, finish);

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
            bool dash = mz % 10f < 5f;
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
        texWall = Tex("Tapeta", 128, 128, (x, y) =>
        {
            float v = (x / 16) % 2 == 0 ? 1f : 0.92f;
            if (((x % 32) - 16) * ((x % 32) - 16) + ((y % 32) - 16) * ((y % 32) - 16) < 9) v = 0.8f;
            return Color.white * v;
        });
        texChecker = Tex("Sachovnica", 64, 64, (x, y) => ((x / 32) + (y / 32)) % 2 == 0 ? Color.white : new Color(0.08f, 0.08f, 0.08f));
        texSoft = Tex("Castica", 64, 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        }, true);
    }

    // 360° panoráma (šírka = dookola, výška = od podlahy po strop, horizont v strede)
    static Texture2D Panorama(string name, Color wall, Color stripe, Color ceiling, Color window, bool stars)
    {
        Color frame = stars ? new Color(0.2f, 0.22f, 0.4f) : new Color(0.97f, 0.96f, 0.93f);
        Color[] pictures = { new Color(0.95f, 0.35f, 0.3f), new Color(0.35f, 0.65f, 0.95f), new Color(0.4f, 0.8f, 0.35f) };
        return Tex(name, 1024, 512, (x, y) =>
        {
            float v = y / 511f;
            if (v < 0.5f) return new Color(0.7f, 0.5f, 0.32f) * (0.9f + 0.1f * H(x / 16, 3));   // podlaha
            if (v < 0.508f) return frame;                                                          // lišta
            if (v > 0.84f) return ceiling;
            if (v > 0.83f) return frame;

            Color c = (x / 24) % 2 == 0 ? wall : stripe;
            // dve okná so šprušľami
            foreach (int cx in new[] { 128, 640 })
            {
                int dx = Mathf.Abs(x - cx);
                if (dx < 70 && v > 0.58f && v < 0.78f)
                {
                    bool edge = dx > 62 || v < 0.592f || v > 0.768f || dx < 4 || Mathf.Abs(v - 0.68f) < 0.008f;
                    c = edge ? frame : window;
                    if (!edge && stars && H(x, y) > 0.985f) c = new Color(1f, 0.95f, 0.6f);
                }
            }
            // obrázky na stene
            for (int i = 0; i < 3; i++)
            {
                int cx = 330 + i * 250;
                if (cx > 600 && cx < 700) continue;
                int dx = Mathf.Abs(x - cx);
                if (dx < 38 && v > 0.62f && v < 0.74f)
                    c = dx > 32 || v < 0.632f || v > 0.728f ? new Color(0.45f, 0.3f, 0.2f) : pictures[i];
            }
            if (stars && H(x, y) > 0.996f) c = new Color(1f, 0.95f, 0.6f);   // svietiace hviezdy na stene
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

    // prostredie: Simple Lit (lacné pre mobil), autá: Lit s leskom (plast)
    // svietiace veci: Unlit (svieti aj v noci; _EMISSION keyword URP pri importe zahadzoval)
    static Material Mat(string name, Color color, Texture2D tex = null, Color? emission = null, bool plastic = false)
    {
        string shader = emission.HasValue ? "Universal Render Pipeline/Unlit"
                      : plastic ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Simple Lit";
        Material m = new Material(Shader.Find(shader));
        m.SetColor("_BaseColor", color);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", plastic ? 0.6f : 0.1f);
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

    class Pal
    {
        public string name;
        public bool night;
        public Material road, curb, carpet, floor, wall, baseboard, window, door, trunk, fence, furniture, lampPost, lampHead, star, castle, cactus;
        public Material[] house, roof, leaves, blocks;
    }

    static Material[] Blocks(string prefix)
    {
        return new[]
        {
            Mat(prefix + "Kocka_Cervena", new Color(0.9f, 0.2f, 0.15f)),
            Mat(prefix + "Kocka_Zlta", new Color(1f, 0.8f, 0.15f)),
            Mat(prefix + "Kocka_Modra", new Color(0.2f, 0.45f, 0.9f)),
            Mat(prefix + "Kocka_Zelena", new Color(0.25f, 0.7f, 0.3f)),
            Mat(prefix + "Kocka_Oranzova", new Color(1f, 0.5f, 0.1f)),
        };
    }

    static Pal CommonRoom(Pal p, Color carpet, Color wall)
    {
        p.road = Mat(p.name + "_Cesta", Color.white, texRoad);
        p.carpet = Mat(p.name + "_Koberec", carpet, texFuzz);
        p.floor = Mat(p.name + "_Parkety", new Color(0.78f, 0.56f, 0.36f), texWood);
        p.wall = Mat(p.name + "_Tapeta", wall, texWall);
        p.baseboard = Mat(p.name + "_Lista", new Color(0.97f, 0.96f, 0.93f));
        p.furniture = Mat(p.name + "_Nabytok", new Color(0.6f, 0.42f, 0.27f), texWood);
        p.trunk = Mat(p.name + "_Kmen", new Color(0.5f, 0.33f, 0.2f));
        p.blocks = Blocks(p.name + "_");
        return p;
    }

    static Pal CityPalette()
    {
        Pal p = CommonRoom(new Pal { name = "Mesto" }, new Color(0.38f, 0.66f, 0.27f), new Color(0.98f, 0.9f, 0.76f));
        p.curb = Mat("Mesto_Obrubnik", new Color(1f, 0.82f, 0.2f));
        p.house = new[]
        {
            Mat("Mesto_Dom_Ruzovy", new Color(1f, 0.72f, 0.72f)),
            Mat("Mesto_Dom_Broskyna", new Color(1f, 0.83f, 0.6f)),
            Mat("Mesto_Dom_Modry", new Color(0.62f, 0.8f, 1f)),
            Mat("Mesto_Dom_Krem", new Color(1f, 0.96f, 0.78f)),
        };
        p.roof = new[]
        {
            Mat("Mesto_Strecha_Fialova", new Color(0.45f, 0.35f, 0.72f)),
            Mat("Mesto_Strecha_Cervena", new Color(0.85f, 0.27f, 0.2f)),
            Mat("Mesto_Strecha_Modra", new Color(0.25f, 0.4f, 0.8f)),
        };
        p.window = Mat("Mesto_Okno", new Color(0.5f, 0.75f, 0.95f));
        p.door = Mat("Mesto_Dvere", new Color(0.55f, 0.32f, 0.2f));
        p.leaves = new[] { Mat("Mesto_Lista1", new Color(0.3f, 0.68f, 0.25f)), Mat("Mesto_Lista2", new Color(0.48f, 0.8f, 0.3f)) };
        p.fence = Mat("Mesto_Plot", new Color(0.98f, 0.98f, 0.96f));
        p.lampPost = Mat("Mesto_Lampa", new Color(0.3f, 0.32f, 0.38f));
        p.lampHead = Mat("Mesto_LampaSvetlo", new Color(1f, 0.95f, 0.8f), null, new Color(0.6f, 0.55f, 0.4f));
        return p;
    }

    static Pal NightPalette()
    {
        Pal p = CommonRoom(new Pal { name = "Noc", night = true }, new Color(0.22f, 0.32f, 0.6f), new Color(0.55f, 0.6f, 0.85f));
        p.curb = Mat("Noc_Obrubnik", new Color(1f, 0.82f, 0.2f));
        p.house = new[]
        {
            Mat("Noc_Dom_Ruzovy", new Color(1f, 0.72f, 0.72f)),
            Mat("Noc_Dom_Modry", new Color(0.62f, 0.8f, 1f)),
            Mat("Noc_Dom_Krem", new Color(1f, 0.96f, 0.78f)),
        };
        p.roof = new[] { Mat("Noc_Strecha_Fialova", new Color(0.45f, 0.35f, 0.72f)), Mat("Noc_Strecha_Cervena", new Color(0.85f, 0.27f, 0.2f)) };
        p.window = Mat("Noc_Okno_Svieti", new Color(1f, 0.85f, 0.4f), null, new Color(1.4f, 1.0f, 0.4f));
        p.door = Mat("Noc_Dvere", new Color(0.55f, 0.32f, 0.2f));
        p.leaves = new[] { Mat("Noc_Lista1", new Color(0.25f, 0.6f, 0.3f)), Mat("Noc_Lista2", new Color(0.35f, 0.7f, 0.35f)) };
        p.fence = Mat("Noc_Plot", new Color(0.9f, 0.9f, 0.95f));
        p.lampPost = Mat("Noc_Lampa", new Color(0.3f, 0.32f, 0.38f));
        p.lampHead = Mat("Noc_LampaSvetlo", new Color(1f, 0.9f, 0.6f), null, new Color(2.2f, 1.8f, 0.9f));
        p.star = Mat("Noc_Hviezda", new Color(1f, 0.95f, 0.5f), null, new Color(1.6f, 1.5f, 0.6f));
        return p;
    }

    static Pal DesertPalette()
    {
        Pal p = CommonRoom(new Pal { name = "Pust" }, new Color(0.95f, 0.82f, 0.56f), new Color(0.72f, 0.87f, 0.97f));
        p.curb = Mat("Pust_Obrubnik", new Color(1f, 0.55f, 0.15f));
        p.castle = Mat("Pust_Hrad", new Color(0.88f, 0.72f, 0.48f));
        p.cactus = Mat("Pust_Kaktus", new Color(0.33f, 0.66f, 0.32f));
        p.leaves = new[] { Mat("Pust_Palma", new Color(0.3f, 0.7f, 0.3f)) };
        p.fence = Mat("Pust_Plot", new Color(0.7f, 0.5f, 0.3f));
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

    static GameObject Box(Transform parent, Material mat, Vector3 pos, Vector3 size, float uvMeters = 2f, float rotY = 0f)
    {
        return Part(parent, BoxMesh(size, uvMeters), mat, pos, Vector3.one, new Vector3(0f, rotY, 0f));
    }

    static Mesh Builtin(string name) { return Resources.GetBuiltinResource<Mesh>(name + ".fbx"); }

    static GameObject Shape(Transform parent, string builtin, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default(Vector3))
    {
        return Part(parent, Builtin(builtin), mat, pos, scale, euler);
    }

    static void Roof(Transform parent, Material mat, Vector3 basePos, float w, float h, float d)
    {
        float x = w / 2f, z = d / 2f;
        Vector3 bl0 = new Vector3(-x, 0f, -z), br0 = new Vector3(x, 0f, -z), t0 = new Vector3(0f, h, -z);
        Vector3 bl1 = new Vector3(-x, 0f, z), br1 = new Vector3(x, 0f, z), t1 = new Vector3(0f, h, z);
        Mesh m = Poly(new List<Vector3[]>
        {
            new[] { bl0, t0, br0 }, new[] { bl1, t1, br1 },
            new[] { bl0, t0, t1, bl1 }, new[] { br0, t0, t1, br1 },
            new[] { bl0, br0, br1, bl1 },
        }, 1f);
        Part(parent, m, mat, basePos, Vector3.one, Vector3.zero);
    }

    // zlúči všetky časti podľa materiálu: z ~100 objektov na úsek ostane ~15 (menej draw callov)
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

    // ───────────────────────── úseky cesty ─────────────────────────

    static float R(Random rnd, float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
    static T Pick<T>(Random rnd, T[] arr) { return arr[rnd.Next(arr.Length)]; }

    static GameObject BuildTheme(string name, Pal p, bool desert)
    {
        GameObject[] segs = new GameObject[Variants];
        for (int i = 0; i < Variants; i++)
        {
            GameObject root = new GameObject("RoadSegment_" + name + "_" + i);
            Random rnd = new Random(name[0] * 131 + i * 7919);
            BuildRoom(root.transform, p, rnd);
            if (desert) BuildDesertDecor(root.transform, p, rnd);
            else BuildTownDecor(root.transform, p, rnd);
            Combine(root, "Seg_" + name + "_" + i);
            segs[i] = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + root.name + ".prefab");
            Object.DestroyImmediate(root);
        }

        GameObject env = new GameObject("Environment_" + name);
        EnvironmentTheme t = env.AddComponent<EnvironmentTheme>();
        RoadSpawner sp = env.AddComponent<RoadSpawner>();
        sp.segments = segs;
        sp.segmentLength = SegLen;

        // namiesto oblohy panoráma detskej izby: tapeta, okná, obrázky, strop; hmla má farbu steny, takže diaľka splynie so stenou
        Color wall, stripe, ceiling, window;
        if (name == "Mesto") { wall = new Color(0.98f, 0.9f, 0.76f); stripe = new Color(0.94f, 0.82f, 0.68f); ceiling = new Color(0.99f, 0.97f, 0.93f); window = new Color(0.6f, 0.8f, 1f); }
        else if (name == "Pust") { wall = new Color(0.74f, 0.88f, 0.97f); stripe = new Color(0.66f, 0.81f, 0.93f); ceiling = new Color(0.97f, 0.98f, 1f); window = new Color(1f, 0.85f, 0.5f); }
        else { wall = new Color(0.13f, 0.15f, 0.32f); stripe = new Color(0.11f, 0.13f, 0.28f); ceiling = new Color(0.07f, 0.08f, 0.17f); window = new Color(0.04f, 0.05f, 0.14f); }
        Texture2D pano = Panorama("Izba_" + name, wall, stripe, ceiling, window, name == "Noc");
        Material sky = new Material(Shader.Find("Skybox/Panoramic"));
        sky.SetTexture("_MainTex", pano);
        sky.SetFloat("_Mapping", 1f);
        sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
        sky.SetFloat("_Exposure", 1f);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));   // 0.5 = bez zmeny farby
        t.fogColor = wall;
        if (name == "Mesto")
        {
            t.ambientSky = new Color(0.85f, 0.82f, 0.78f); t.ambientEquator = new Color(0.7f, 0.68f, 0.62f); t.ambientGround = new Color(0.45f, 0.42f, 0.38f);
            t.sunColor = new Color(1f, 0.96f, 0.88f); t.sunIntensity = 1.15f; t.sunRotation = new Vector3(50f, -35f, 0f);
        }
        else if (name == "Pust")
        {
            t.fogStart = 60f; t.fogEnd = 210f;
            t.ambientSky = new Color(0.95f, 0.88f, 0.75f); t.ambientEquator = new Color(0.8f, 0.72f, 0.6f); t.ambientGround = new Color(0.55f, 0.48f, 0.38f);
            t.sunColor = new Color(1f, 0.92f, 0.78f); t.sunIntensity = 1.35f; t.sunRotation = new Vector3(55f, 30f, 0f);
        }
        else
        {
            t.fogStart = 25f; t.fogEnd = 150f;
            t.ambientSky = new Color(0.22f, 0.25f, 0.45f); t.ambientEquator = new Color(0.14f, 0.15f, 0.3f); t.ambientGround = new Color(0.06f, 0.06f, 0.12f);
            t.sunColor = new Color(0.55f, 0.65f, 1f); t.sunIntensity = 0.4f; t.sunRotation = new Vector3(40f, -20f, 0f);
        }
        t.skybox = Save(sky, Art + "/Materials/" + name + "_Obloha.mat");

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(env, PrefabDir + "/Environment_" + name + ".prefab");
        Object.DestroyImmediate(env);
        return prefab;
    }

    // cesta, obrubník, koberec, parkety, steny izby
    static void BuildRoom(Transform r, Pal p, Random rnd)
    {
        // cesta: textúra 8 m × 10 m, u = (x + 4) / 8, v = z / 10
        Mesh road = new Mesh();
        road.vertices = new[] { new Vector3(-4f, 0f, 0f), new Vector3(-4f, 0f, SegLen), new Vector3(4f, 0f, SegLen), new Vector3(4f, 0f, 0f) };
        road.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, SegLen / 10f), new Vector2(1f, SegLen / 10f), new Vector2(1f, 0f) };
        road.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        road.RecalculateNormals();
        road.RecalculateBounds();
        Part(r, road, p.road, Vector3.zero, Vector3.one, Vector3.zero);

        for (int s = -1; s <= 1; s += 2)
        {
            Box(r, p.curb, new Vector3(s * 4.1f, 0.04f, 10f), new Vector3(0.2f, 0.08f, SegLen));
            Box(r, p.carpet, new Vector3(s * 22.1f, -0.03f, 10f), new Vector3(35.8f, 0.06f, SegLen), 2f);
            Box(r, p.wall, new Vector3(s * 85.5f, 20f, 10f), new Vector3(1f, 40f, SegLen), 6f);
            Box(r, p.baseboard, new Vector3(s * 84.8f, 0.5f, 10f), new Vector3(0.4f, 1.1f, SegLen));
        }
        Box(r, p.floor, new Vector3(0f, -0.1f, 10f), new Vector3(170f, 0.08f, SegLen), 4f);

        // obrovské veci v izbe (vidno ich z diaľky cez hmlu)
        if (rnd.NextDouble() < 0.65) Giant(r, p, rnd, rnd.Next(2) == 0 ? -1 : 1);

        // toy blocks rozhádzané po koberci
        for (int i = 0; i < 3; i++)
        {
            int s = rnd.Next(2) == 0 ? -1 : 1;
            float size = R(rnd, 0.9f, 1.6f);
            Box(r, Pick(rnd, p.blocks), new Vector3(s * R(rnd, 17f, 38f), size / 2f, R(rnd, 2f, 18f)), Vector3.one * size, 1f, R(rnd, 0f, 90f));
        }
    }

    static void Giant(Transform r, Pal p, Random rnd, int s)
    {
        int type = rnd.Next(p.night ? 6 : 5);
        switch (type)
        {
            case 0: // veža z obrích kociek
                for (int i = 0; i < 3; i++)
                    Box(r, Pick(rnd, p.blocks), new Vector3(s * R(rnd, 55f, 62f), 3.5f + i * 7f, 10f), Vector3.one * 7f, 3f, R(rnd, -15f, 15f));
                break;
            case 1: // lopta
                Shape(r, "Sphere", Pick(rnd, p.blocks), new Vector3(s * R(rnd, 52f, 68f), 7f, 10f), Vector3.one * 14f);
                break;
            case 2: // stôl (4 nohy a doska)
                foreach (float lx in new[] { 52f, 78f })
                    foreach (float lz in new[] { 2f, 18f })
                        Box(r, p.furniture, new Vector3(s * lx, 16f, lz), new Vector3(2.5f, 32f, 2.5f), 4f);
                Box(r, p.furniture, new Vector3(s * 65f, 32.8f, 10f), new Vector3(31f, 1.6f, SegLen), 4f);
                break;
            case 3: // pastelka
                Material c = Pick(rnd, p.blocks);
                float x = s * R(rnd, 48f, 65f);
                Shape(r, "Cylinder", c, new Vector3(x, 1.4f, 9f), new Vector3(2.8f, 7f, 2.8f), new Vector3(90f, 0f, 0f));
                Shape(r, "Sphere", c, new Vector3(x, 1.4f, 16.2f), new Vector3(2.8f, 2.8f, 4f));
                Shape(r, "Cylinder", p.baseboard, new Vector3(x, 1.4f, 7f), new Vector3(2.9f, 2.5f, 2.9f), new Vector3(90f, 0f, 0f));
                break;
            case 4: // kopa kníh
                for (int i = 0; i < 4; i++)
                    Box(r, Pick(rnd, p.blocks), new Vector3(s * R(rnd, 58f, 62f), 0.9f + i * 1.8f, 10f), new Vector3(R(rnd, 9f, 12f), 1.8f, R(rnd, 6f, 8f)), 2f, R(rnd, -12f, 12f));
                break;
            default: // nočná lampička mesiac na stene
                Shape(r, "Sphere", p.star, new Vector3(s * 84f, 14f, 10f), new Vector3(2f, 8f, 8f));
                break;
        }
    }

    static void BuildTownDecor(Transform r, Pal p, Random rnd)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            // biely plot pri ceste
            if (rnd.NextDouble() < 0.5)
            {
                for (float z = 0.4f; z < SegLen; z += 0.8f)
                    Box(r, p.fence, new Vector3(s * 5.4f, 0.45f, z), new Vector3(0.12f, 0.9f, 0.12f));
                Box(r, p.fence, new Vector3(s * 5.4f, 0.3f, 10f), new Vector3(0.06f, 0.1f, SegLen));
                Box(r, p.fence, new Vector3(s * 5.4f, 0.7f, 10f), new Vector3(0.06f, 0.1f, SegLen));
            }

            // domček
            if (rnd.NextDouble() < 0.85)
            {
                float w = R(rnd, 4f, 6f), h = R(rnd, 3f, 4.5f), d = R(rnd, 4f, 6f);
                float x = s * (9f + w / 2f + R(rnd, 0f, 3f));
                float z = R(rnd, d / 2f + 0.5f, SegLen - d / 2f - 0.5f);
                float face = x - s * (w / 2f + 0.03f);
                Box(r, Pick(rnd, p.house), new Vector3(x, h / 2f, z), new Vector3(w, h, d), 1f);
                Roof(r, Pick(rnd, p.roof), new Vector3(x, h, z), w + 0.6f, h * 0.5f, d + 0.6f);
                Box(r, p.door, new Vector3(face, 0.8f, z), new Vector3(0.06f, 1.6f, 0.9f));
                foreach (float dz in new[] { -0.3f, 0.3f })
                    Box(r, p.window, new Vector3(face, h * 0.62f, z + dz * d), new Vector3(0.06f, 0.8f, 0.8f));
                foreach (float dx in new[] { -0.25f, 0.25f })
                    Box(r, p.window, new Vector3(x + dx * w, h * 0.6f, z - d / 2f - 0.03f), new Vector3(0.8f, 0.8f, 0.06f));
                Box(r, p.blocks[0], new Vector3(x + w * 0.2f, h * 1.35f, z + d * 0.2f), new Vector3(0.5f, 1.1f, 0.5f));
            }

            // okrúhle stromčeky
            int trees = rnd.Next(2, 4);
            for (int i = 0; i < trees; i++)
            {
                float x = s * R(rnd, 6.3f, 8f), z = R(rnd, 1f, 19f), c = R(rnd, 1.6f, 2.3f);
                Shape(r, "Cylinder", p.trunk, new Vector3(x, 0.6f, z), new Vector3(0.28f, 0.6f, 0.28f));
                Shape(r, "Sphere", Pick(rnd, p.leaves), new Vector3(x, 1.1f + c * 0.45f, z), Vector3.one * c);
            }

            // pouličná lampa
            if (s == (rnd.Next(2) == 0 ? -1 : 1) || p.night)
            {
                Shape(r, "Cylinder", p.lampPost, new Vector3(s * 4.8f, 1.5f, 10f), new Vector3(0.15f, 1.5f, 0.15f));
                Box(r, p.lampPost, new Vector3(s * 4.4f, 3.0f, 10f), new Vector3(0.8f, 0.1f, 0.1f));
                Shape(r, "Sphere", p.lampHead, new Vector3(s * 4.05f, 2.85f, 10f), Vector3.one * 0.45f);
            }
        }

        // veža z kociek (červeno-žltá ako na obrázku)
        {
            int s = rnd.Next(2) == 0 ? -1 : 1;
            float x = s * R(rnd, 20f, 32f), z = R(rnd, 4f, 16f);
            int n = rnd.Next(3, 8);
            for (int i = 0; i < n; i++)
                Box(r, p.blocks[i % 2], new Vector3(x, 1f + i * 2f, z), Vector3.one * 2f, 1f, R(rnd, -8f, 8f));
        }

        // svietiace hviezdičky na koberci a stenách
        if (p.night)
        {
            for (int i = 0; i < 6; i++)
            {
                int s = rnd.Next(2) == 0 ? -1 : 1;
                Box(r, p.star, new Vector3(s * R(rnd, 10f, 38f), 0.01f, R(rnd, 1f, 19f)), new Vector3(0.6f, 0.02f, 0.6f), 1f, 45f);
                Box(r, p.star, new Vector3(s * 84.95f, R(rnd, 8f, 36f), R(rnd, 1f, 19f)), new Vector3(0.1f, 0.9f, 0.9f), 1f);
            }
        }
    }

    static void BuildDesertDecor(Transform r, Pal p, Random rnd)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            // kaktusy
            for (int i = 0; i < 2; i++)
            {
                float x = s * R(rnd, 6.3f, 10f), z = R(rnd, 1f, 19f);
                Shape(r, "Capsule", p.cactus, new Vector3(x, 1.3f, z), new Vector3(0.7f, 1.3f, 0.7f));
                Shape(r, "Capsule", p.cactus, new Vector3(x + 0.55f, 1.6f, z), new Vector3(0.35f, 0.5f, 0.35f));
                Shape(r, "Capsule", p.cactus, new Vector3(x - 0.55f, 1.2f, z), new Vector3(0.35f, 0.45f, 0.35f));
            }

            // hrad z piesku
            if (rnd.NextDouble() < 0.6)
            {
                float x = s * R(rnd, 13f, 18f), z = R(rnd, 5f, 15f);
                Box(r, p.castle, new Vector3(x, 1f, z), new Vector3(5f, 2f, 5f), 1f);
                foreach (float dx in new[] { -2.5f, 2.5f })
                    foreach (float dz in new[] { -2.5f, 2.5f })
                        Shape(r, "Cylinder", p.castle, new Vector3(x + dx, 1.6f, z + dz), new Vector3(1.2f, 1.6f, 1.2f));
                Shape(r, "Cylinder", p.trunk, new Vector3(x, 3f, z), new Vector3(0.08f, 1f, 0.08f));
                Box(r, p.blocks[0], new Vector3(x + 0.4f, 3.7f, z), new Vector3(0.8f, 0.5f, 0.05f));
            }

            // vedierko a lopatka
            if (rnd.NextDouble() < 0.6)
            {
                float x = s * R(rnd, 9f, 14f), z = R(rnd, 2f, 18f);
                Material c = Pick(rnd, p.blocks);
                Shape(r, "Cylinder", c, new Vector3(x, 0.7f, z), new Vector3(1.2f, 0.7f, 1.2f));
                Box(r, c, new Vector3(x + 1.5f, 0.06f, z + 0.5f), new Vector3(0.12f, 0.12f, 1.6f), 1f, 25f);
                Box(r, c, new Vector3(x + 1.85f, 0.04f, z + 1.3f), new Vector3(0.6f, 0.06f, 0.7f), 1f, 25f);
            }

            // palma
            {
                float x = s * R(rnd, 20f, 32f), z = R(rnd, 3f, 17f);
                for (int i = 0; i < 5; i++)
                    Shape(r, "Cylinder", p.trunk, new Vector3(x + s * i * 0.15f, 0.5f + i * 1f, z), new Vector3(0.5f - i * 0.04f, 0.5f, 0.5f - i * 0.04f));
                Vector3 top = new Vector3(x + s * 0.75f, 5.2f, z);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f;
                    Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    Shape(r, "Sphere", p.leaves[0], top + dir * 1.4f + Vector3.down * 0.3f, new Vector3(0.9f, 0.25f, 3f), new Vector3(15f, a, 0f));
                }
            }

            // stupňovitá pyramída
            if (rnd.NextDouble() < 0.5)
            {
                float x = s * R(rnd, 26f, 36f), z = R(rnd, 5f, 15f);
                for (int i = 0; i < 4; i++)
                    Box(r, p.castle, new Vector3(x, 0.6f + i * 1.2f, z), new Vector3(7f - i * 1.6f, 1.2f, 7f - i * 1.6f), 1f);
            }

            // drevený plôtik
            if (rnd.NextDouble() < 0.35)
            {
                for (float z = 1f; z < SegLen; z += 2f)
                    Box(r, p.fence, new Vector3(s * 5.4f, 0.4f, z), new Vector3(0.15f, 0.8f, 0.15f));
                Box(r, p.fence, new Vector3(s * 5.4f, 0.6f, 10f), new Vector3(0.08f, 0.12f, SegLen));
            }
        }
    }

    // ───────────────────────── štart / cieľ ─────────────────────────

    static GameObject BuildLine(string name, bool gantry)
    {
        Material checker = Mat("Sachovnica", Color.white, texChecker);
        Material red = Mat("Brana_Cervena", new Color(0.9f, 0.2f, 0.15f));
        Material yellow = Mat("Brana_Zlta", new Color(1f, 0.8f, 0.15f));

        GameObject root = new GameObject(name);
        Box(root.transform, checker, new Vector3(0f, 0.012f, 0f), new Vector3(8f, 0.02f, 1.5f), 1f);
        if (gantry)
        {
            // brána z kociek ako na obrázku: červeno-žlté stĺpy, šachovnicový pás
            foreach (float x in new[] { -5.2f, 5.2f })
                for (int i = 0; i < 5; i++)
                    Box(root.transform, i % 2 == 0 ? red : yellow, new Vector3(x, 0.5f + i, 0f), Vector3.one, 1f);
            Box(root.transform, checker, new Vector3(0f, 5.4f, 0f), new Vector3(11.4f, 0.8f, 0.4f), 0.8f);
        }
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

    static void BuildCars()
    {
        Material glass = Mat("Auto_Sklo", new Color(0.15f, 0.22f, 0.35f), null, null, true);
        Material tyre = Mat("Auto_Pneumatika", new Color(0.12f, 0.12f, 0.13f));
        Material hub = Mat("Auto_Disk", new Color(0.85f, 0.85f, 0.88f), null, null, true);
        Material dark = Mat("Auto_Naraznik", new Color(0.25f, 0.25f, 0.28f));
        Material head = Mat("Auto_Svetlo", new Color(1f, 0.95f, 0.75f), null, new Color(1.5f, 1.4f, 1f));
        Material tail = Mat("Auto_Koncove", new Color(1f, 0.15f, 0.1f), null, new Color(1.2f, 0.1f, 0.05f));
        Material white = Mat("Auto_Biela", new Color(0.97f, 0.97f, 0.97f), null, null, true);
        Material smoke = ParticleMat("Fx_Dym", false);
        Material flame = ParticleMat("Fx_Nitro", true);

        CarParts parts = new CarParts { glass = glass, tyre = tyre, hub = hub, dark = dark, head = head, tail = tail, white = white, smoke = smoke, flame = flame };

        BuildCar("Assets/Prefabs/Car.prefab", parts, Mat("Auto_Hrac", new Color(0.9f, 0.15f, 0.12f), null, null, true), CarKind.Player, null);
        BuildCar("Assets/Prefabs/EnemyCar (1).prefab", parts, Mat("Auto_Super1", new Color(1f, 0.6f, 0.1f), null, null, true), CarKind.Enemy, null);
        BuildCar("Assets/Prefabs/EnemyCar (2).prefab", parts, Mat("Auto_Super2", new Color(0.2f, 0.42f, 0.88f), null, null, true), CarKind.Enemy, null);
        BuildCar("Assets/Prefabs/EnemyCar (3).prefab", parts, Mat("Auto_Super3", new Color(0.2f, 0.6f, 0.3f), null, null, true), CarKind.Enemy, null);
        Material[] vans =
        {
            Mat("Auto_Dodavka_Biela", new Color(0.95f, 0.95f, 0.93f), null, null, true),
            Mat("Auto_Dodavka_Zlta", new Color(1f, 0.85f, 0.25f), null, null, true),
            Mat("Auto_Dodavka_Fialova", new Color(0.6f, 0.4f, 0.85f), null, null, true),
            Mat("Auto_Dodavka_Tyrkys", new Color(0.2f, 0.75f, 0.75f), null, null, true),
            Mat("Auto_Dodavka_Ruzova", new Color(1f, 0.55f, 0.7f), null, null, true),
        };
        BuildCar("Assets/Prefabs/TrafficCar.prefab", parts, vans[0], CarKind.Traffic, vans);
    }

    enum CarKind { Player, Enemy, Traffic }

    class CarParts { public Material glass, tyre, hub, dark, head, tail, white, smoke, flame; }

    // Mení iba potomka "Model": koreň, jeho Box Collider (1.6 × 1 × 3.5), Rigidbody a skripty ostávajú.
    static void BuildCar(string path, CarParts m, Material paint, CarKind kind, Material[] variants)
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

        // koreň je v strede collidera (y = 0.5 nad cestou), spodok auta je teda y = -0.5
        GameObject body;
        if (kind == CarKind.Traffic)
        {
            body = Shape(model, "Cube", paint, new Vector3(0f, 0.02f, -0.05f), new Vector3(1.45f, 0.8f, 3.3f));
            Shape(model, "Cube", m.glass, new Vector3(0f, 0.2f, 0.3f), new Vector3(1.47f, 0.3f, 2.2f));
            Shape(model, "Cube", m.glass, new Vector3(0f, 0.2f, 1.6f), new Vector3(1.3f, 0.32f, 0.04f));
        }
        else
        {
            body = Shape(model, "Cube", paint, new Vector3(0f, -0.12f, 0f), new Vector3(1.4f, 0.42f, 3.4f));
            GameObject cabin = Shape(model, "Cube", paint, new Vector3(0f, 0.26f, -0.25f), new Vector3(1.2f, 0.4f, 1.7f));
            Shape(model, "Cube", m.glass, new Vector3(0f, 0.27f, -0.25f), new Vector3(1.22f, 0.26f, 1.72f));
            if (kind == CarKind.Player)
            {
                Shape(model, "Cube", m.white, new Vector3(0f, 0.095f, 0.95f), new Vector3(0.3f, 0.02f, 1.5f));
                Shape(model, "Cube", m.white, new Vector3(0f, 0.465f, -0.25f), new Vector3(0.3f, 0.02f, 1.6f));
                Shape(model, "Cube", m.dark, new Vector3(-0.45f, 0.25f, -1.55f), new Vector3(0.08f, 0.32f, 0.1f));
                Shape(model, "Cube", m.dark, new Vector3(0.45f, 0.25f, -1.55f), new Vector3(0.08f, 0.32f, 0.1f));
                Shape(model, "Cube", paint, new Vector3(0f, 0.42f, -1.58f), new Vector3(1.35f, 0.06f, 0.3f));
            }
            cabin.name = "Kabina";
        }
        body.name = "Karoseria";

        Shape(model, "Cube", m.dark, new Vector3(0f, -0.28f, 1.68f), new Vector3(1.44f, 0.16f, 0.12f));
        Shape(model, "Cube", m.dark, new Vector3(0f, -0.28f, -1.68f), new Vector3(1.44f, 0.16f, 0.12f));
        foreach (float x in new[] { -0.45f, 0.45f })
        {
            Shape(model, "Cube", m.head, new Vector3(x, -0.05f, 1.72f), new Vector3(0.3f, 0.14f, 0.04f));
            Shape(model, "Cube", m.tail, new Vector3(x, -0.05f, -1.72f), new Vector3(0.3f, 0.12f, 0.04f));
        }

        List<Transform> wheels = new List<Transform>();
        foreach (float x in new[] { -0.69f, 0.69f })
            foreach (float z in new[] { -1.1f, 1.1f })
            {
                Transform pivot = new GameObject("Koleso").transform;
                pivot.SetParent(model, false);
                pivot.localPosition = new Vector3(x, -0.18f, z);
                Shape(pivot, "Cylinder", m.tyre, Vector3.zero, new Vector3(0.64f, 0.11f, 0.64f), new Vector3(0f, 0f, 90f));
                Shape(pivot, "Cylinder", m.hub, Vector3.zero, new Vector3(0.34f, 0.115f, 0.34f), new Vector3(0f, 0f, 90f));
                Shape(pivot, "Cube", m.dark, new Vector3(Mathf.Sign(x) * 0.118f, 0f, 0f), new Vector3(0.01f, 0.3f, 0.07f));   // lúč, aby bolo vidno točenie
                wheels.Add(pivot);
            }

        foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>())
            if (r.sharedMaterial == m.head || r.sharedMaterial == m.tail) r.shadowCastingMode = ShadowCastingMode.Off;

        CarEffects fx = model.gameObject.AddComponent<CarEffects>();
        fx.wheels = wheels.ToArray();
        fx.wheelRadius = 0.32f;
        fx.body = body.GetComponent<MeshRenderer>();
        fx.bodyVariants = variants;
        fx.smoke = Fx(model, "Dym", new Vector3(0.4f, -0.3f, -1.75f), m.smoke, 0.9f, 0.6f, 0.25f, new Color(0.85f, 0.85f, 0.85f, 0.45f), 6f, 3f, true);
        if (kind == CarKind.Player)
        {
            ParticleSystem n = Fx(model, "Nitro", new Vector3(0f, -0.2f, -1.78f), m.flame, 0.18f, 7f, 0.45f, new Color(1f, 0.55f, 0.15f, 0.9f), 70f, 0.2f, false);
            fx.nitro = n;
        }
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
    public static void Screenshots()
    {
        try
        {
            GenerateAll();
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
                for (int i = -3; i < 12; i++)
                    Object.Instantiate(sp.segments[(i + 3) % sp.segments.Length], new Vector3(0f, 0f, i * SegLen), Quaternion.identity);

                Camera cam = Camera.main;
                Transform car = Object.FindAnyObjectByType<CarController>().transform;
                cam.transform.position = car.position + new Vector3(0f, 4f, -8f);
                cam.transform.LookAt(car.position + Vector3.forward * 5f);
                Shot(cam, 1280, 720, dir + "/" + t + "_landscape.png");
                Shot(cam, 720, 1280, dir + "/" + t + "_portrait.png");

                cam.transform.position = car.position + new Vector3(2.5f, 1.6f, 4.5f);
                cam.transform.LookAt(car.position);
                Shot(cam, 1280, 720, dir + "/" + t + "_auto.png");
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
