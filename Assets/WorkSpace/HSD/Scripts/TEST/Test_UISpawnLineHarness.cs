using UnityEngine;

public class Test_UISpawnLineHarness : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UISpawnLine linePrefab;
    [SerializeField] private RectTransform startPoint;
    [SerializeField] private RectTransform endPoint;
    [SerializeField] private Transform parent;

    [Header("Settings")]
    public float testCurveHeight = 100f;
    public float testDuration = 0.5f;
    public float testWidth = 20f;

    [ContextMenu("Test Fire")]
    [Button]
    public void TestFire()
    {
        if (linePrefab == null || startPoint == null || endPoint == null)
        {
            Debug.LogError("Test_UISpawnLineHarness: Missing references!");
            return;
        }

        Transform p = parent != null ? parent : transform;
        UISpawnLine line = Instantiate(linePrefab, p);
        
        // Reset scale and position
        line.rectTransform.anchoredPosition = Vector2.zero;
        line.rectTransform.localScale = Vector3.one;

        // Calculate local positions
        Vector2 startLocal = p.InverseTransformPoint(startPoint.position);
        Vector2 endLocal = p.InverseTransformPoint(endPoint.position);

        Debug.Log($"Testing SpawnLine: Start {startLocal}, End {endLocal}");
        
        line.width = testWidth;
        line.duration = testDuration;
        line.curveHeight = testCurveHeight;
        line.color = Color.white; // Ensure visibility

        line.Fire(startLocal, endLocal);

        // Auto destroy after test
        Destroy(line.gameObject, testDuration + 0.5f);
    }
}
