using System;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class PlayerChiefManager
{
    private const string TABLE_NAME = "PlayerChiefData";
    private const string DATA_KEY = "SelectedChiefId";

    public int SelectedChiefId
    {
        get => selectedChiefId; 
        private set 
        {
            if (selectedChiefId == value) return;

            selectedChiefId = value;
            ChangeSelectId?.Invoke(selectedChiefId); 
        }
    }
    private int selectedChiefId;
    private string _rowInDate = string.Empty;

    public Action<int> ChangeSelectId; 

    public async UniTask InitializeAsync()
    {
        bool isInit = false;
        Load(() => isInit = true);
        await UniTask.WaitUntil(() => isInit);
    }

    public void SetSelectedChief(int id)
    {
        SelectedChiefId = id;
        Save();
    }

    public void Save()
    {
        Param param = new Param();
        param.Add(DATA_KEY, SelectedChiefId);

        if (string.IsNullOrEmpty(_rowInDate))
        {
            // 데이터가 없는 경우 최초 생성 (Insert)
            Backend.GameData.Insert(TABLE_NAME, param, callback =>
            {
                if (callback.IsSuccess())
                {
                    _rowInDate = callback.GetInDate();
                    Debug.Log("족장 데이터 최초 저장 성공");
                }
                else
                {
                    Debug.LogError($"족장 데이터 최초 저장 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            });
        }
        else
        {
            // 기존 데이터 업데이트 (Update)
            Backend.GameData.UpdateV2(TABLE_NAME, _rowInDate, Backend.UserInDate, param, callback =>
            {
                if (callback.IsSuccess())
                {
                    Debug.Log("족장 데이터 업데이트 성공");
                }
                else
                {
                    Debug.LogError($"족장 데이터 업데이트 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
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
                        _rowInDate = rows[0]["inDate"].ToString();

                        if (rows[0].ContainsKey(DATA_KEY) && rows[0][DATA_KEY] != null)
                        {
                            SelectedChiefId = int.Parse(rows[0][DATA_KEY].ToString());
                        }
                        Debug.Log($"족장 데이터 로드 성공: SelectedChiefId={SelectedChiefId}");
                    }
                    else
                    {
                        Debug.Log("저장된 족장 데이터가 없습니다. 기본값으로 처리합니다.");
                        SelectedChiefId = 0; // 기본 족장 ID
                    }
                }
                else
                {
                    Debug.LogError($"족장 데이터 로드 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"족장 데이터 파싱 중 예외 발생: {e.Message}");
            }
            finally
            {
                onCompleted?.Invoke();
            }
        });
    }
}
