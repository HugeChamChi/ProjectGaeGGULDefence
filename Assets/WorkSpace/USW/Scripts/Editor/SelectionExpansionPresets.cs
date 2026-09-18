using System;
using UnityEditor;
using UnityEngine;

/// <summary>추가 선택지 7종의 설정 및 별도 SO 초안 생성. 기존 풀은 작성자가 연결한다.</summary>
public static class SelectionExpansionPresets
{
    /// <summary>추가 선택지 순서(0~6)로 설정한다. 기존 ID와 표시 자산은 보존한다.</summary>
    public static void Configure(LevelUpData card, int index)
    {
        if (index < 0 || index > 6) throw new ArgumentOutOfRangeException(nameof(index));
        if (index < 4)
        {
            DroneSelectionPresets.Configure(card, (DroneSelectionKind)((int)DroneSelectionKind.ZeltanColdStorage + index));
            return;
        }
        card.primaryEffect = card.secondaryEffect = LevelUpEffectType.None;
        card.droneEffect = null;
        card.spawnRate = 1f;
        card.specialValue = 0f;
        card.tier = Tier.Rare;
        switch (index)
        {
            case 4:
                card.chooseName = "오류코드 0x00";
                card.description = "다음 선택지 한 번은 확정적으로 에픽 등급이 나옵니다.";
                card.specialEffect = LevelUpSpecialEffect.GuaranteeNextEpic;
                break;
            case 5:
                card.chooseName = "새 상품, 뜯지 않음.";
                card.description = "강화 비용이 10% 감소하고, 이번 런에서 지금까지 강화에 실제로 쓴 식량의 10%를 환급받습니다.";
                card.tier = Tier.Normal;
                card.specialEffect = LevelUpSpecialEffect.UpgradeDiscountAndRefund;
                card.specialValue = 10f;
                break;
            case 6:
                card.chooseName = "계약 무효";
                card.description = "즉시 등급을 다시 추첨하여 선택지 3장을 새로 뽑고, 선택 제한시간을 초기화합니다.";
                card.specialEffect = LevelUpSpecialEffect.RerollChoices;
                break;
        }
    }

    /// <summary>선택한 새 경로에 7개 SO와 초안 풀을 생성한다. 자동으로 족장 풀에 합치지 않는다.</summary>
    [MenuItem("Tools/Selections/Create Seven Selection Drafts")]
    public static void CreateDrafts()
    {
        string path = EditorUtility.SaveFilePanelInProject("추가 선택지 초안", "SelectionExpansionPool", "asset", "새 경로를 지정하세요.");
        if (string.IsNullOrEmpty(path)) return;
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            throw new InvalidOperationException("Choose a new path.");
        string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var pool = ScriptableObject.CreateInstance<LevelUpPoolData>();
        pool.PoolId = 9200;
        pool.Cards = new LevelUpData[7];
        for (int i = 0; i < pool.Cards.Length; i++)
        {
            var card = ScriptableObject.CreateInstance<LevelUpData>();
            Configure(card, i);
            card.chooseId = 9201 + i;
            AssetDatabase.CreateAsset(card, AssetDatabase.GenerateUniqueAssetPath(folder + "/SelectionExpansion" + (i + 1) + ".asset"));
            pool.Cards[i] = card;
        }
        AssetDatabase.CreateAsset(pool, path);
        AssetDatabase.SaveAssetIfDirty(pool);
        Selection.activeObject = pool;
    }
}
