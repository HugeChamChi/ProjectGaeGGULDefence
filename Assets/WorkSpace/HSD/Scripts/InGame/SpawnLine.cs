using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SpawnLine : MonoBehaviour
{
    [Header("Settings")]
    public float duration = 0.4f;
    public bool UseUnscaledTime { get; set; }
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

    /// <summary>소환 경로를 재생한다. 선행 단계의 초과 시간은 같은 예정 도착 시각에 맞춰 반영한다.</summary>
    public void Fire(Vector3 from, Vector3 to, float elapsedSeconds = 0f)
    {
        startPos = from;
        endPos = to;

        controlPoint = Vector3.Lerp(from, to, 0.5f) + Vector3.up * curveHeight;

        StartCoroutine(AnimateLine(Mathf.Max(0f, elapsedSeconds)));
    }

    private IEnumerator AnimateLine(float elapsedSeconds)
    {
        double started = UseUnscaledTime ? Time.unscaledTimeAsDouble : Time.timeAsDouble;
        float elapsed = elapsedSeconds;
        lr.enabled = true;

        while (elapsed < duration)
        {
            float headT = Mathf.Clamp01(elapsed / duration);
            float tailT = Mathf.Clamp01(headT - lineLength);

            DrawLineBetween(tailT, headT);
            yield return null;
            elapsed = elapsedSeconds + (float)((UseUnscaledTime ? Time.unscaledTimeAsDouble : Time.timeAsDouble) - started);
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
