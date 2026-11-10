using System.Collections.Generic;
using UnityEngine;

// Nekonečná cesta: úseky pred hráčom pridáva, staré za ním presúva dopredu (pool, nič sa nemaže ani nevytvára počas hry).
// Iba číta pozíciu hráča, autom ani kamerou nehýbe.
public class RoadSpawner : MonoBehaviour
{
    public CarController player;          // ak je prázdne, nájde sa samo
    public GameObject[] segments;         // varianty úsekov (pivot na začiatku úseku, úsek ide od z = 0 po z = segmentLength)
    public float segmentLength = 20f;
    public int ahead = 10;                // počet úsekov pred hráčom
    public int behind = 3;                // počet úsekov za hráčom

    readonly Queue<Transform> pool = new Queue<Transform>();
    float nextZ;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<CarController>();
        float startZ = player != null ? player.transform.position.z : 0f;
        nextZ = Mathf.Floor(startZ / segmentLength - behind) * segmentLength;

        for (int i = 0; i < ahead + behind; i++)
        {
            GameObject prefab = segments[Random.Range(0, segments.Length)];
            Transform t = Instantiate(prefab, transform).transform;
            Place(t);
        }
    }

    void Update()
    {
        if (player == null) { player = FindAnyObjectByType<CarController>(); return; }
        float limit = player.transform.position.z + ahead * segmentLength;

        // najstarší úsek je vždy aspoň "behind" úsekov za hráčom, takže ho môžeme presunúť dopredu
        while (nextZ < limit)
            Place(pool.Dequeue());
    }

    void Place(Transform t)
    {
        t.position = new Vector3(0f, 0f, nextZ);
        nextZ += segmentLength;
        pool.Enqueue(t);
    }
}
