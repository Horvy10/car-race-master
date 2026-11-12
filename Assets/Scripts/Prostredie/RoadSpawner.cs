using System.Collections.Generic;
using UnityEngine;

// Nekonečná cesta: úsek, ktorý ostal za hráčom, ide do zásoby a dopredu sa položí náhodný iný (nič sa nemaže ani nevytvára počas hry).
// Iba číta pozíciu hráča, autom ani kamerou nehýbe.
public class RoadSpawner : MonoBehaviour
{
    public CarController player;          // ak je prázdne, nájde sa samo
    public GameObject[] segments;         // varianty úsekov (pivot na začiatku úseku, úsek ide od z = 0 po z = segmentLength)
    public float segmentLength = 40f;
    public int ahead = 6;                 // počet úsekov pred hráčom
    public int behind = 2;                // počet úsekov za hráčom (kamera je za autom, pod ňou nesmie vzniknúť diera)

    readonly Queue<Transform> active = new Queue<Transform>();
    readonly List<Transform> spare = new List<Transform>();
    float nextZ;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<CarController>();
        float startZ = player != null ? player.transform.position.z : 0f;
        nextZ = Mathf.Floor(startZ / segmentLength - behind) * segmentLength;

        // z každého variantu dva kusy, aby v zásobe vždy bolo z čoho vyberať
        int total = Mathf.Max(segments.Length * 2, ahead + behind + 2);
        for (int i = 0; i < total; i++)
        {
            Transform t = Instantiate(segments[i % segments.Length], transform).transform;
            t.gameObject.SetActive(false);
            spare.Add(t);
        }
        for (int i = 0; i < ahead + behind; i++) Place();
    }

    void Update()
    {
        if (player == null) { player = FindAnyObjectByType<CarController>(); return; }
        float limit = player.transform.position.z + ahead * segmentLength;

        while (nextZ < limit)
        {
            Transform old = active.Dequeue();
            old.gameObject.SetActive(false);
            spare.Add(old);
            Place();
        }
    }

    void Place()
    {
        int i = Random.Range(0, spare.Count);
        Transform t = spare[i];
        spare.RemoveAt(i);

        // otočenie o 180° okolo stredu úseku: z každého variantu sú dva rôzne úseky (ľavá strana vpravo a odzadu)
        bool turned = Random.value < 0.5f;
        t.SetPositionAndRotation(new Vector3(0f, 0f, turned ? nextZ + segmentLength : nextZ), Quaternion.Euler(0f, turned ? 180f : 0f, 0f));
        t.gameObject.SetActive(true);
        nextZ += segmentLength;
        active.Enqueue(t);
    }
}
