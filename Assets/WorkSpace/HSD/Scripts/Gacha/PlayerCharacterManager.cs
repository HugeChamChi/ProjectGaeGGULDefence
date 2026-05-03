using System;
using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class PlayerCharacterManager
{
    private Dictionary<int, int> _ownedCharacterCounts = new Dictionary<int, int>();

    const string TABLE_NAME = "PlayerOwnedCharacterData";
    const string DATA_KEY = "OwnedCharacterCounts";
    private string rowInDate = string.Empty;

    public async UniTask InitalizeAsync()
    {
        bool isInit = false;

        Load(() => isInit = true);

        await UniTask.WaitUntil(() => isInit);
    }

    public int GetCount(int characterId)
    {
        return _ownedCharacterCounts.TryGetValue(characterId, out int count) ? count : 0;
    }
    public void AddCharacter(int characterId, int count = 1)
    {
        if (_ownedCharacterCounts.ContainsKey(characterId))
        {
            _ownedCharacterCounts[characterId] += count;
        }
        else
        {
            _ownedCharacterCounts[characterId] = count;
        }
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

    public void Save()
    {
        Param param = new Param();

        // Dictionary<int, int>를 Backend에 저장하기 위해 Dictionary<string, int>로 변환
        Dictionary<string, int> saveMap = new Dictionary<string, int>();
        foreach (var pair in _ownedCharacterCounts)
        {
            saveMap.Add(pair.Key.ToString(), pair.Value);
        }

        param.Add(DATA_KEY, saveMap);

        if (string.IsNullOrEmpty(rowInDate))
        {
            // 데이터가 없는 경우 최초 생성 (Insert)
            Backend.GameData.Insert(TABLE_NAME, param, callback =>
            {
                if (callback.IsSuccess())
                {
                    rowInDate = callback.GetInDate();
                    Debug.Log("캐릭터 보유 데이터 최초 저장 성공");
                }
                else
                {
                    Debug.LogError($"캐릭터 보유 데이터 최초 저장 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            });
        }
        else
        {
            // 기존 데이터 업데이트 (Update)
            Backend.GameData.UpdateV2(TABLE_NAME, rowInDate, Backend.UserInDate, param, callback =>
            {
                if (callback.IsSuccess())
                {
                    Debug.Log("캐릭터 보유 데이터 업데이트 성공");
                }
                else
                {
                    Debug.LogError($"캐릭터 보유 데이터 업데이트 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            });
        }
    }

    public void Load(Action onCompleted = null)
    {
        Backend.GameData.GetMyData(TABLE_NAME, new Where(), callback =>
        {
            try
            {
                if (callback.IsSuccess())
                {
                    JsonData rows = callback.FlattenRows();

                    if (rows.Count > 0)
                    {
                        // 데이터가 존재하는 경우 복구
                        rowInDate = rows[0]["inDate"].ToString();

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