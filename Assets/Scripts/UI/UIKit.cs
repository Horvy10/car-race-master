using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// UI sa skladá v kóde: hlavné scény netreba ručne upravovať, takže v Gite nevznikajú konflikty v scénach.
public static class UIKit
{
    public static readonly Color Panel = new Color(0.08f, 0.09f, 0.13f, 0.9f);
    public static readonly Color Accent = new Color(1f, 0.55f, 0.1f);
    public static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.35f);

    static Font font;
    public static Font Font
    {
        get { return font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")); }
    }

    public static Transform Canvas(string name)
    {
        GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        return go.transform;
    }

    // anchor = bod rodiča, ku ktorému je prvok prilepený (0,0 = vľavo dole, 0.5,0.5 = stred, 1,1 = vpravo hore)
    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Box(Transform parent, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Image img = Rect(parent, "Box", anchor, pos, size).gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // Panel cez celú obrazovku (pauza, výsledok, obchod, rebríček)
    public static GameObject Overlay(Transform parent, string name, Color color)
    {
        RectTransform rt = Rect(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.gameObject.AddComponent<Image>().color = color;
        return rt.gameObject;
    }

    public static Text Label(Transform parent, string text, int fontSize, Vector2 anchor, Vector2 pos, Vector2 size,
                             TextAnchor align = TextAnchor.MiddleCenter)
    {
        Text t = Rect(parent, "Text", anchor, pos, size).gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button AddButton(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
    {
        RectTransform rt = Rect(parent, text, anchor, pos, size);
        rt.gameObject.AddComponent<Image>();
        Button b = rt.gameObject.AddComponent<Button>();
        SetColor(b, ButtonColor);
        Label(rt, text, 36, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        b.onClick.AddListener(() => onClick());
        return b;
    }

    public static void SetColor(Button b, Color color)
    {
        ColorBlock c = b.colors;
        c.normalColor = color;
        c.selectedColor = color;
        c.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
        c.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        c.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
        b.colors = c;
    }

    public static void SetText(Button b, string text)
    {
        b.GetComponentInChildren<Text>().text = text;
    }

    public static InputField Input(Transform parent, string value, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Rect(parent, "Input", anchor, pos, size);
        rt.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);
        InputField input = rt.gameObject.AddComponent<InputField>();

        Text text = Label(rt, "", 36, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(30f, 0f));
        text.color = Color.black;
        text.supportRichText = false;

        input.textComponent = text;
        input.characterLimit = 16;
        input.text = value;
        return input;
    }
}
