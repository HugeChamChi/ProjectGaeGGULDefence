using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SpawnLine : MonoBehaviour
{
    [Header("Settings")]
    public float duration = 0.4f;
    public float lineLength = 0.3f;
    public int resolution = 20;
    public int numCapVertices = 5;
    public float widthMultiplier = 0.3f;

    [Header("Curve Control")]
    public float curveHeight = 5f;

    private LineRenderer lr;
    private Vector3 startPos;
    private Vector3 endPos;
    private Vector3 controlPoint;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = resolution;
        lr.useWorldSpace = true;

        SetWidthCurve();
    }

    public void Fire(Vector3 from, Vector3 to)
    {
        startPos = from;
        endPos = to;

        controlPoint = Vector3.Lerp(from, to, 0.5f) + Vector3.up * curveHeight;

        StartCoroutine(AnimateLine());
    }

    private IEnumerator AnimateLine()
    {
        float elapsed = 0f;
        lr.enabled = true;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float headT = Mathf.Clamp01(elapsed / duration);
            float tailT = Mathf.Clamp01(headT - lineLength);

            DrawLineBetween(tailT, headT);
            yield return null;
        }

        RM.Destroy(gameObject);
    }

    private void DrawLineBetween(float t0, float t1)
    {
        for (int i = 0; i < resolution; i++)
        {
            float t = Mathf.Lerp(t1, t0, (float)i / (resolution - 1));
            Vector3 pos = MathUtils.Bezier(startPos, controlPoint, endPos, t);
            lr.SetPosition(i, pos);
        }
    }

    private void SetWidthCurve()
    {
        lr.widthMultiplier = widthMultiplier;
        lr.numCapVertices = numCapVertices;
    }
}