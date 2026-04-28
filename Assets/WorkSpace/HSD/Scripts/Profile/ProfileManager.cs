using BackEnd;
using LitJson;
using UnityEngine;

public class ProfileManager
{
    public ProfileData Data { get; private set; } = new();
    private string rowInDate = string.Empty;

    const string TABLE_NAME = "PlayerProfile";

    public void Save()
    {
        Param param = Data.ToParam();

        if (string.IsNullOrEmpty(rowInDate))
        {
            // 데이터가 없는 경우 최초 생성 (Insert)
            Backend.GameData.Insert(TABLE_NAME, param, callback => {
                if (callback.IsSuccess())
                {
                    rowInDate = callback.GetInDate();
                    Debug.Log("프로필 데이터 최초 저장 성공");
                }
                else
                {
                    Debug.LogError($"프로필 데이터 최초 저장 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            });
        }
        else
        {
            // 기존 데이터 업데이트 (Update)
            Backend.GameData.UpdateV2(TABLE_NAME, rowInDate, Backend.UserInDate, param, callback => {
                if (callback.IsSuccess())
                {
                    Debug.Log("프로필 데이터 업데이트 성공");
                }
                else
                {
                    Debug.LogError($"프로필 데이터 업데이트 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            });
        }
    }

    public void Load()
    {
        Backend.GameData.GetMyData(TABLE_NAME, new Where(), callback => {
            if (callback.IsSuccess())
            {
                JsonData rows = callback.FlattenRows();

                if (rows.Count > 0)
                {
                    // 데이터가 존재하는 경우 복구
                    rowInDate = rows[0]["inDate"].ToString();
                    Data.FromData(rows[0]);
                    Debug.Log("프로필 데이터 로드 및 복구 성공");
                }
                else
                {
                    // 데이터가 없는 경우 (신규 유저)
                    Debug.Log("저장된 프로필 데이터가 없습니다. 신규 유저로 처리합니다.");
                }
            }
            else
            {
                // 네트워크 오류, 서버 점검 등 실패 처리
                Debug.LogError($"프로필 데이터 로드 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
            }
        });
    }
}
