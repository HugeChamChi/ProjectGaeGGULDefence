using UnityEngine;
using VContainer;

/// <summary>
/// CurrencyFloaterManager 기능을 검증하기 위한 테스트 스크립트
/// </summary>
public class Test_CurrencyFloater : MonoBehaviour
{
    [Inject] private CurrencyFloaterManager _currencyFloaterManager;

    [Header("테스트 설정")]
    public float testAmount = 100f;
    public Vector3 testPosition = Vector3.zero;

    [ContextMenu("Spawn Floater at Origin")]
    [Button]
    public void TestSpawnAtOrigin()
    {
        if (_currencyFloaterManager != null)
        {
            _currencyFloaterManager.SpawnCurrencyText(Vector3.zero, testAmount);
            Debug.Log("[Test_CurrencyFloater] Spawned floater at (0, 0, 0)");
        }
        else
        {
            Debug.LogError("[Test_CurrencyFloater] CurrencyFloaterManager instance not found!");
        }
    }

    [ContextMenu("Spawn Floater at Random Position")]
    [Button]
    public void TestSpawnRandom()
    {
        if (_currencyFloaterManager != null)
        {
            Vector3 randomPos = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0);
            _currencyFloaterManager.SpawnCurrencyText(randomPos, testAmount);
            Debug.Log($"[Test_CurrencyFloater] Spawned floater at {randomPos}");
        }
    }
}
