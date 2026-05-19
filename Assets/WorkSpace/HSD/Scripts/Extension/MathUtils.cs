using UnityEngine;

public static class MathUtils
{
    public static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
    }
}
