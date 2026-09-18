using UnityEngine;

/// <summary>
/// 레벨업에 필요한 경험치 테이블. 구글 시트(GameDataManager._expTable) 대신 이 SO가 정본이다.
/// 인덱스 0 = 1레벨에서 2레벨로 올라가는 데 필요한 경험치.
/// </summary>
[CreateAssetMenu(fileName = "ExpLevelData", menuName = "Game/ExpLevelData")]
public class ExpLevelData : ScriptableObject
{
    [SerializeField] private float[] expRequired = new float[]
    {
        1, 10, 20, 30, 50,
        100, 150, 200, 250, 300,
        350, 400, 450, 500, 550,
        600, 650, 700, 750, 800
    };

    public int LevelCount => expRequired?.Length ?? 0;

    public float GetExpRequired(int level)
    {
        if (expRequired == null || expRequired.Length == 0) return 100f;
        int idx = Mathf.Clamp(level - 1, 0, expRequired.Length - 1);
        return expRequired[idx];
    }
}
