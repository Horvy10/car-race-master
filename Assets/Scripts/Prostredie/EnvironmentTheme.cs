using UnityEngine;
using UnityEngine.Rendering;

// Nastavenie oblohy, hmly a svetla pre jednu tému. Leží na koreni prefabu Environment_*.
public class EnvironmentTheme : MonoBehaviour
{
    public Material skybox;
    public Color fogColor = new Color(0.85f, 0.8f, 0.72f);
    public float fogStart = 50f;
    public float fogEnd = 190f;

    [Header("Okolité svetlo")]
    public Color ambientSky = new Color(0.75f, 0.75f, 0.8f);
    public Color ambientEquator = new Color(0.6f, 0.6f, 0.6f);
    public Color ambientGround = new Color(0.35f, 0.35f, 0.3f);

    [Header("Slnko (Directional Light v scéne)")]
    public Color sunColor = Color.white;
    public float sunIntensity = 1.2f;
    public Vector3 sunRotation = new Vector3(50f, -30f, 0f);

    void Awake() { Apply(); }

    public void Apply()
    {
        if (skybox != null) RenderSettings.skybox = skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.color = sunColor;
            l.intensity = sunIntensity;
            l.transform.rotation = Quaternion.Euler(sunRotation);
            l.shadows = LightShadows.Soft;
        }
    }
}
