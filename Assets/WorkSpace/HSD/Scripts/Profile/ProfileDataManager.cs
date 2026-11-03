using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class ProfileDataManager : Global.IClearable
{
    private const string TABLE_NAME = "PlayerProfile";

    public PlayerProfileData Data { get; private set; } = new();
    public bool IsDirty { get => false; set { } }
    private string _owner;
    private int _generation;
    private bool _loaded;
    /// <summary>Current account profile has been read successfully.</summary>
    public bool IsReady => _loaded && !string.IsNullOrEmpty(_owner) && _owner == Backend.UserInDate;
    /// <summary>Selection changes, including account clear/load.</summary>
    public event Action OnChanged;

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


    public async UniTask InitalizeAsync()
    {
        var tcs = new UniTaskCompletionSource();
        Load(() => tcs.TrySetResult());
        await tcs.Task;
    }

    public void Clear()
    {
        _generation++; _owner = null; _loaded = false;
        Data = new();
        OnChanged?.Invoke();
    }

    /// <summary>Stages only owned catalog choices; SO default grants are never modified.</summary>
    public bool TrySelect(int iconId, int frameId)
    {
        if (!IsReady || !Owned(Table.Profile.GetIcon(iconId), Data.iconStatusMap) || !Owned(Table.Profile.GetFrame(frameId), Data.frameStatusMap)) return false;
        if (Data.CurrentIconId != iconId || Data.CurrentFrameId != frameId)
        {
            if (!Player.PlayerData.SelectProfile(iconId, frameId)) return false;
            Data.CurrentIconId = iconId; Data.CurrentFrameId = frameId;
            OnChanged?.Invoke();
        }
        return true;
    }

    private static bool Owned(IProfileItem item, System.Collections.Generic.Dictionary<string, bool> map) =>
        item != null && (item.IsUnlocked || (map.TryGetValue(item.Id.ToString(), out bool unlocked) && unlocked));

    /// <inheritdoc />
    public async UniTask SaveAsync() => await TrySaveAsync(CancellationToken.None);

    /// <summary>Selections share PlayerData; legacy profile rows retain ownership only.</summary>
    public async UniTask<bool> TrySaveAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return IsReady && await Player.PlayerData.TrySaveAsync().AttachExternalCancellation(token);
    }

    private void ApplySavedSelection()
    {
        if (!Player.PlayerData.IsReady) return;
        var data = Player.PlayerData.Data;
        if (data.SelectedIconId >= 0) Data.CurrentIconId = data.SelectedIconId;
        if (data.SelectedFrameId >= 0) Data.CurrentFrameId = data.SelectedFrameId;
    }

    public void Load(Action onCompleted = null)
    {
        int generation = ++_generation;
        string owner = Backend.UserInDate;
        _owner = owner; _loaded = false;
        Backend.GameData.GetMyData(TABLE_NAME, new Where(), callback =>
        {
            try
            {
                if (generation != _generation || owner != Backend.UserInDate) return;
                if (callback.IsSuccess())
                {
                    JsonData rows = callback.FlattenRows();
                    if (rows.Count > 1) { Debug.LogWarning("[Profile] Multiple account rows; selection disabled."); return; }

                    if (rows.Count > 0)
                    {
                        // 데이터가 존재하는 경우 복구
                        Data.FromData(rows[0]);
                        _loaded = true; ApplySavedSelection();
                        OnChanged?.Invoke();
                        Debug.Log($"프로필 데이터 로드 성공: IconId={Data.CurrentIconId}, FrameId={Data.CurrentFrameId}");
                    }
                    else
                    {
                        Data = new(); _loaded = true; ApplySavedSelection();
                        // 데이터가 없는 경우 (신규 유저)
                        Debug.Log("저장된 프로필 데이터가 없습니다. 신규 유저로 처리합니다.");
                    }
                }
                else
                {
                    // 네트워크 오류, 서버 점검 등 실패 처리
                    Debug.LogError($"프로필 데이터 로드 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"프로필 데이터 파싱 중 예외 발생: {e.Message}");
            }
            finally
            {
                onCompleted?.Invoke();
            }
        });
    }
}
