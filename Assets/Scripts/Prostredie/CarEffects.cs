using UnityEngine;

// Vizuál auta: točenie kolies, dym z výfuku, nitro, prach. Leží na potomkovi "Model", autom nehýbe, iba číta rýchlosť.
public class CarEffects : MonoBehaviour
{
    public Transform[] wheels;
    public float wheelRadius = 0.32f;
    public ParticleSystem smoke;
    public ParticleSystem nitro;
    public ParticleSystem[] dust;
    public GameObject[] models;           // premávka: náhodný typ auta (taxi, autobus...) z tejto sady

    CarController player;
    EnemyCar enemy;
    TrafficCar traffic;

    void Awake()
    {
        player = GetComponentInParent<CarController>();
        enemy = GetComponentInParent<EnemyCar>();
        traffic = GetComponentInParent<TrafficCar>();

        if (models != null && models.Length > 0)
        {
            int k = Random.Range(0, models.Length);
            for (int i = 0; i < models.Length; i++) models[i].SetActive(i == k);
        }
    }

    void Update()
    {
        float speed = player != null ? player.CurrentSpeed
                    : enemy != null ? enemy.CurrentSpeed
                    : traffic != null ? traffic.Speed : 0f;

        float spin = speed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime;
        if (wheels != null)
            foreach (Transform w in wheels)
                w.Rotate(spin, 0f, 0f, Space.Self);

        if (smoke != null)
        {
            var e = smoke.emission;
            e.rateOverTime = Mathf.Lerp(3f, 18f, speed / 30f);
        }

        // nitro zrýchli auto nad maxSpeed, inak sa tam nedostane
        if (nitro != null)
        {
            var e = nitro.emission;
            e.enabled = player != null && speed > player.maxSpeed + 0.5f;
        }

        if (dust == null) return;
        foreach (ParticleSystem d in dust)
        {
            var e = d.emission;
            e.enabled = speed > 5f;
        }
    }
}
