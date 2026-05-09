using UnityEngine;
using UnityEditor;

/// <summary>
/// Tools > Create Wave Data Assets 실행 시
/// Assets/WorkSpace/USW/Data/WaveData/ 에 WaveData 에셋 10개 일괄 생성.
/// 시트 출처: https://docs.google.com/spreadsheets/d/11mzpFO8G1SQl3vZfcT0n9wAYHb8h4y7zdolLK0IofVg
///
/// 생성 후 각 WaveData Inspector에서 bosses[0].prefab 에 보스 프리팹 연결 필요.
/// </summary>
public static class WaveDataCreator
{
    private const string OutputPath = "Assets/WorkSpace/USW/Data/WaveData";

    // 시트 기준: base_hp=50000, hp_increment=15000
    private static readonly int[] TotalHp =
    {
         50000,  // Round 1
         65000,  // Round 2
         80000,  // Round 3
         95000,  // Round 4
        110000,  // Round 5
        125000,  // Round 6
        140000,  // Round 7
        155000,  // Round 8
        170000,  // Round 9
        185000,  // Round 10
    };

    [MenuItem("Tools/Create Wave Data Assets")]
    public static void CreateAll()
    {
        EnsureFolder();

        int created = 0;

        for (int i = 0; i < TotalHp.Length; i++)
        {
            int round = i + 1;
            string path = $"{OutputPath}/WaveData_Round{round:D2}.asset";

            if (AssetDatabase.LoadAssetAtPath<WaveData>(path) != null)
            {
                Debug.LogWarning($"[WaveDataCreator] 이미 존재 — 스킵: WaveData_Round{round:D2}.asset");
                continue;
            }

            var asset = ScriptableObject.CreateInstance<WaveData>();
            asset.waveName = $"Round {round}";
            asset.bosses   = new BossEntry[]
            {
                new BossEntry { hp = TotalHp[i] }
            };

            AssetDatabase.CreateAsset(asset, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"{OutputPath}\n에 {created}개 WaveData 에셋이 생성되었습니다.\n\n각 에셋의 bosses[0].prefab 에 보스 프리팹을 연결해주세요.";
        Debug.Log($"[WaveDataCreator] 완료 — {created}개 생성");
        EditorUtility.DisplayDialog("생성 완료", msg, "확인");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/WorkSpace/USW/Data"))
            AssetDatabase.CreateFolder("Assets/WorkSpace/USW", "Data");

        if (!AssetDatabase.IsValidFolder(OutputPath))
            AssetDatabase.CreateFolder("Assets/WorkSpace/USW/Data", "WaveData");
    }
}
