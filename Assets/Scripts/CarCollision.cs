using UnityEngine;

public static class CarCollision
{
    public static readonly Vector3 CarHalfExtents = new Vector3(0.78f, 0.45f, 1.7f);

    // true, ak by pohyb na newPos v smere moveDir narazil do auta, ktoré je v tomto smere
    public static bool Blocked(Transform self, Vector3 newPos, Vector3 moveDir, out float blockerSpeed)
    {
        blockerSpeed = 0f;
        Physics.SyncTransforms();

        Collider[] hits = Physics.OverlapBox(newPos, CarHalfExtents, Quaternion.identity, ~0,
                                             QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
        {
            Transform other;
            float speed;
            if (!GetCar(c, self, out other, out speed)) continue;

            // blokuje nás len auto, ktoré je v smere nášho pohybu
            Vector3 delta = other.position - self.position;
            if (Vector3.Dot(delta, moveDir) > 0.1f)
            {
                blockerSpeed = speed;
                return true;
            }
        }
        return false;
    }

    // je v danej oblasti iné auto? (kontrola susedného pruhu)
    public static bool AreaOccupied(Transform self, Vector3 center, Vector3 halfExtents)
    {
        Physics.SyncTransforms();

        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0,
                                             QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
        {
            Transform other;
            float speed;
            if (GetCar(c, self, out other, out speed)) return true;
        }
        return false;
    }

    static bool GetCar(Collider c, Transform self, out Transform other, out float speed)
    {
        other = null;
        speed = 0f;

        EnemyCar e = c.GetComponentInParent<EnemyCar>();
        if (e != null)
        {
            other = e.transform;
            speed = e.CurrentSpeed;
        }
        else
        {
            CarController p = c.GetComponentInParent<CarController>();
            if (p != null)
            {
                other = p.transform;
                speed = p.CurrentSpeed;
            }
        }
        return other != null && other != self;
    }
}