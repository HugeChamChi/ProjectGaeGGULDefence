using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>메인 스레드에서 PlayerPrefs를 사용하는 로컬 구현. 디스크 쓰기는 동기이다.</summary>
public sealed class PlayerPrefsResearchSaveStore : IResearchSaveStore
{
    /// <inheritdoc />
    public async UniTask<ResearchSaveData> LoadAsync(ResearchSaveAddress address, CancellationToken cancellationToken)
    {
        await UniTask.SwitchToMainThread(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (PlayerPrefs.HasKey(address.StorageKey))
                return Parse(PlayerPrefs.GetString(address.StorageKey), address.TreeKey, false);
            string legacy = address.LegacyKey;
            if (address.AccountKey != ResearchSaveAddress.LocalAccount || string.IsNullOrEmpty(legacy) ||
                !PlayerPrefs.HasKey(legacy) || PlayerPrefs.HasKey(MigrationKey(legacy)))
                return new ResearchSaveData { TreeKey = address.TreeKey };
            var migrated = Parse(PlayerPrefs.GetString(legacy), address.TreeKey, true);
            cancellationToken.ThrowIfCancellationRequested();
            // 기존 원본은 남기고 새 형식과 완료 표시를 함께 저장한다.
            PlayerPrefs.SetString(address.StorageKey, JsonUtility.ToJson(migrated));
            PlayerPrefs.SetString(MigrationKey(legacy), address.StorageKey);
            PlayerPrefs.Save();
            return migrated;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Debug.LogWarning("[Research] 강화 저장 읽기 실패. 원본을 보존합니다: " + error.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async UniTask SaveAsync(ResearchSaveAddress address, ResearchSaveData data, CancellationToken cancellationToken)
    {
        data.GetLevels(address.TreeKey);
        string json = JsonUtility.ToJson(data);
        await UniTask.SwitchToMainThread(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (PlayerPrefs.HasKey(address.StorageKey)) Parse(PlayerPrefs.GetString(address.StorageKey), address.TreeKey, false);
        PlayerPrefs.SetString(address.StorageKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>기존 저장 하나가 여러 트리/계정으로 복제되지 않게 하는 완료 키.</summary>
    public static string MigrationKey(string legacyKey) => "OutgameUpgrade.Migrated." + Uri.EscapeDataString(legacyKey);

    private static ResearchSaveData Parse(string json, string treeKey, bool legacy)
    {
        // 누락 필드의 기본값을 정상 저장으로 오인하지 않도록 JSON 구조부터 검사한다.
        var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (root["Entries"] is not JArray entries) throw new InvalidDataException("Entries가 없는 강화 저장입니다.");
        var data = new ResearchSaveData { TreeKey = treeKey };
        if (legacy)
        {
            if (root["SchemaVersion"] != null) throw new InvalidDataException("기존 키에 예상하지 않은 버전이 있습니다.");
        }
        else if (root["SchemaVersion"]?.Type != JTokenType.Integer || (long)root["SchemaVersion"] != ResearchSaveData.CurrentVersion ||
                 root["TreeKey"]?.Type != JTokenType.String || (string)root["TreeKey"] != treeKey)
            throw new InvalidDataException("지원하지 않는 강화 저장 버전 또는 TreeKey입니다.");
        foreach (var item in entries)
        {
            if (item is not JObject entry || entry["Id"]?.Type != JTokenType.String || entry["Level"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("잘못된 노드 저장 형식입니다.");
            data.Entries.Add(new ResearchSaveData.Entry { Id = (string)entry["Id"], Level = checked((int)(long)entry["Level"]) });
        }
        data.GetLevels(treeKey);
        return data;
    }
}
