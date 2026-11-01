using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class PlayerCharacterManager : Global.IClearable
{
    private const string TABLE_NAME = "PlayerOwnedCharacterData";
    private const string DATA_KEY = "OwnedCharacterCounts";
    private Dictionary<int, int> _ownedCharacterCounts = new Dictionary<int, int>();
    public bool IsDirty { get => false; set { } }
    private int _generation;
    private string _owner;


    public async UniTask InitalizeAsync()
    {
        var tcs = new UniTaskCompletionSource();
        Load(() => tcs.TrySetResult());
        await tcs.Task;
        if (string.IsNullOrEmpty(_owner) || _owner != Backend.UserInDate)
            throw new InvalidOperationException("Character inventory could not be loaded.");
    }

    public void Clear()
    {
        _generation++; _owner = null;
        _ownedCharacterCounts.Clear();
    }

    public int GetCount(int characterId)
    {
        int existing = _ownedCharacterCounts.TryGetValue(characterId, out int count) ? count : 0;
        int rewards = Player.PlayerData.IsReady ? Player.PlayerData.Data.RewardInventory.Count(RewardType.Unit, characterId) : 0;
        return (int)Math.Min(int.MaxValue, (long)existing + rewards);
    }
    public void AddCharacter(int characterId, int count = 1)
    {
        // Legacy counts remain read-only. New grants share PlayerData's durable inventory snapshot.
        if (_owner != Backend.UserInDate) return;
        Player.PlayerData.AddOwnedCharacter(characterId, count);
    }
    public void AddCharacters(int[] ids)
    {
        foreach (var id in ids)
        {
            AddCharacter(id, 1);
        }
    }
    public void AddCharacter(ICharacterData character)
    {
        AddCharacter(character.Id, 1);
    }
    public void AddCharacters(ICharacterData[] characters)
    {
        foreach (var character in characters)
        {
            AddCharacter(character.Id, 1);
        }
    }

    public UniTask SaveAsync() => Player.PlayerData.SaveAsync();

    public void Load(Action onCompleted = null)
    {
        Clear();
        int generation = _generation;
        string owner = Backend.UserInDate;
        Backend.GameData.GetMyData(TABLE_NAME, new Where(), callback =>
        {
            try
            {
                if (generation != _generation || owner != Backend.UserInDate) return;
                if (callback.IsSuccess())
                {
                    JsonData rows = callback.FlattenRows();
                    if (rows.Count > 1) throw new InvalidOperationException("Character duplicate rows require repair.");

                    if (rows.Count > 0)
                    {
                        // 데이터가 존재하는 경우 복구

                        if (rows[0].ContainsKey(DATA_KEY) && rows[0][DATA_KEY] != null)
                        {
                            _ownedCharacterCounts.Clear();
                            JsonData data = rows[0][DATA_KEY];
                            foreach (string key in data.Keys)
                            {
                                if (int.TryParse(key, out int id))
                                {
                                    _ownedCharacterCounts.Add(id, (int)data[key]);
                                }
                            }
                        }
                        Debug.Log($"캐릭터 보유 데이터 로드 성공: Count={_ownedCharacterCounts.Count}");
                    }
                    else
                    {
                        // 데이터가 없는 경우 (신규 유저)
                        Debug.Log("저장된 캐릭터 보유 데이터가 없습니다. 신규 유저로 처리합니다.");
                    }
                    _owner = owner;
                }
                else
                {
                    // 네트워크 오류, 서버 점검 등 실패 처리
                    Debug.LogError($"캐릭터 보유 데이터 로드 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"캐릭터 보유 데이터 파싱 중 예외 발생: {e.Message}");
            }
            finally
            {
                onCompleted?.Invoke();
            }
        });
    }
}
