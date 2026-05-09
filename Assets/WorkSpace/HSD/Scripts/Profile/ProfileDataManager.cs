using System;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class ProfileDataManager
{
    private const string TABLE_NAME = "PlayerProfile";

    public PlayerProfileData Data { get; private set; } = new();

    public Sprite CurrentIconSprite
    {
        get
        {
            var iconData = Table.Profile.GetIcon(Data.CurrentIconId);
            if (iconData == null)
            {
                Debug.LogWarning($"아이콘 데이터가 없습니다. CurrentIconId: {Data.CurrentIconId}");
                return null;
            }
            return iconData.Sprite;
        }
    }
    public Sprite CurrentFrameSprite
    {
        get
        {
            var frameData = Table.Profile.GetFrame(Data.CurrentFrameId);
            if (frameData == null)
            {
                Debug.LogWarning($"프레임 데이터가 없습니다. CurrentFrameId: {Data.CurrentFrameId}");
                return null;
            }
            return frameData.Sprite;
        }
    }

    public IProfileItem CurrentIconData => Table.Profile.GetIcon(Data.CurrentIconId);
    public IProfileItem CurrentFrameData => Table.Profile.GetFrame(Data.CurrentFrameId);

    private string rowInDate = string.Empty;

    public async UniTask InitalizeAsync()
    {
        bool isInit = false;

        Load(() => isInit = true);

        await UniTask.WaitUntil(() => isInit);
    }

    public void Save()
    {
        Param param = Data.ToParam();

        if (string.IsNullOrEmpty(rowInDate))
        {
            // 데이터가 없는 경우 최초 생성 (Insert)
            Backend.GameData.Insert(TABLE_NAME, param, callback =>
            {
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
            Backend.GameData.UpdateV2(TABLE_NAME, rowInDate, Backend.UserInDate, param, callback =>
            {
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

    public void Load(Action onCompleted = null)
    {
        Backend.GameData.GetMyData(TABLE_NAME, new Where(), callback =>
        {
            if (callback.IsSuccess())
            {
                JsonData rows = callback.FlattenRows();

                if (rows.Count > 0)
                {
                    // 데이터가 존재하는 경우 복구
                    rowInDate = rows[0]["inDate"].ToString();
                    Data.FromData(rows[0]);
                    Debug.Log($"프로필 데이터 로드 성공: IconId={Data.CurrentIconId}, FrameId={Data.CurrentFrameId}");
                }
                else
                {
                    // 데이터가 없는 경우 (신규 유저)
                    Debug.Log("저장된 프로필 데이터가 없습니다. 신규 유저로 처리합니다.");
                }

                onCompleted?.Invoke();
            }
            else
            {
                // 네트워크 오류, 서버 점검 등 실패 처리
                Debug.LogError($"프로필 데이터 로드 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
            }
        });
    }
}
