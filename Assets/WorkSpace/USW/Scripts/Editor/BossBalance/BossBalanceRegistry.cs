using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>엑셀의 고정 키와 Unity 에셋 참조를 연결하는 에디터 전용 등록표.</summary>
public sealed class BossBalanceRegistry : ScriptableObject
{
    /// <summary>보스 수치 키와 수정 대상 SO.</summary>
    [Serializable]
    public sealed class BossBinding
    {
        public string Key;
        public BossData Data;
    }

    /// <summary>외형 키와 BossBase 프리팹.</summary>
    [Serializable]
    public sealed class PrefabBinding
    {
        public string Key;
        public GameObject Prefab;
    }

    /// <summary>기본 등록표 위치. 에디터 에셋이므로 플레이어에 포함되지 않는다.</summary>
    public const string AssetPath = "Assets/WorkSpace/USW/Scripts/Editor/BossBalance/BossBalanceRegistry.asset";
    [SerializeField] private List<BossBinding> _bosses = new();
    [SerializeField] private List<PrefabBinding> _prefabs = new();
    /// <summary>수치 키 목록.</summary>
    public IReadOnlyList<BossBinding> Bosses => _bosses;
    /// <summary>프리팹 키 목록.</summary>
    public IReadOnlyList<PrefabBinding> Prefabs => _prefabs;

    /// <summary>키는 대소문자를 구분하며 공백 없는 영문/숫자/밑줄로 관리한다.</summary>
    public static bool IsValidKey(string key) => key != null && Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");

    /// <summary>기본 등록표를 불러온다. 최초 등록 시에만 생성한다.</summary>
    public static BossBalanceRegistry GetOrCreate()
    {
        var registry = AssetDatabase.LoadAssetAtPath<BossBalanceRegistry>(AssetPath);
        if (registry != null) return registry;
        registry = CreateInstance<BossBalanceRegistry>();
        AssetDatabase.CreateAsset(registry, AssetPath);
        return registry;
    }

    /// <summary>새 에셋만 등록한다. 기존 키/참조와 중복된 프리팹 사용은 유지한다.</summary>
    public void RegisterExistingAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 후 등록하세요.");
        Undo.RecordObject(this, "Register boss balance assets");
        foreach (string guid in AssetDatabase.FindAssets("t:BossData", new[] { "Assets/WorkSpace/USW/Data/BossData" }).OrderBy(x => x))
        {
            var data = AssetDatabase.LoadAssetAtPath<BossData>(AssetDatabase.GUIDToAssetPath(guid));
            if (_bosses.All(x => x.Data != data))
                _bosses.Add(new BossBinding { Key = AvailableKey(data.name, _bosses.Select(x => x.Key)), Data = data });
            AddPrefab(data.Prefab);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/WorkSpace/USW/Prefab/BossUnit" }).OrderBy(x => x))
            AddPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        _bosses.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        _prefabs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssetIfDirty(this);
    }

    private void AddPrefab(GameObject prefab)
    {
        if (prefab == null || prefab.GetComponent<BossBase>() == null || _prefabs.Any(x => x.Prefab == prefab)) return;
        _prefabs.Add(new PrefabBinding { Key = AvailableKey(prefab.name, _prefabs.Select(x => x.Key)), Prefab = prefab });
    }

    private static string AvailableKey(string name, IEnumerable<string> used)
    {
        string stem = Regex.Replace(name, "[^A-Za-z0-9_]", "_");
        if (!IsValidKey(stem)) stem = "Boss_" + stem;
        var keys = new HashSet<string>(used, StringComparer.Ordinal);
        string key = stem;
        for (int suffix = 2; keys.Contains(key); suffix++) key = stem + "_" + suffix;
        return key;
    }
}
