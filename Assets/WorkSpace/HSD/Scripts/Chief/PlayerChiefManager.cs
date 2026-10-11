using System;
using System.Globalization;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

public class PlayerChiefManager : Global.IClearable
{
    private const string TABLE_NAME = "PlayerChiefData";
    private const string DATA_KEY = "SelectedChiefId";
    public bool IsDirty { get; set; }

    public int SelectedChiefId
    {
        get => selectedChiefId; 
        private set 
        {
            if (selectedChiefId == value) return;

            selectedChiefId = value;
            NotifySelectionChanged();
        }
    }
    private int selectedChiefId;
    private string _rowInDate = string.Empty;
    private bool _loadSucceeded;

    public Action<int> ChangeSelectId;
    public ChiefData SelectChiefData => Table.Character.Chief.GetChief(selectedChiefId);

    public async UniTask InitializeAsync()
    {
        var tcs = new UniTaskCompletionSource();
        Load(() => tcs.TrySetResult());
        await tcs.Task;
    }

    public void Clear()
    {
        selectedChiefId = 0;
        _rowInDate = string.Empty;
        IsDirty = false;
        _loadSucceeded = false;
    }

    public void SetSelectedChief(int id)
    {
        SelectedChiefId = id;
        IsDirty = true;
    }

    public async UniTask SaveAsync()
    {
        if (!IsDirty) return;
        // A failed read must not turn an unknown existing row into a duplicate Insert.
        if (!_loadSucceeded)
        {
            Debug.LogWarning("족장 데이터 로드가 완료되지 않아 저장을 보류합니다.");
            return;
        }

        Param param = new Param();
        param.Add(DATA_KEY, SelectedChiefId);
        var tcs = new UniTaskCompletionSource();

        if (string.IsNullOrEmpty(_rowInDate))
        {
            // 데이터가 없는 경우 최초 생성 (Insert)
            Backend.GameData.Insert(TABLE_NAME, param, callback =>
            {
                if (callback.IsSuccess())
                {
                    _rowInDate = callback.GetInDate();
                    Debug.Log("족장 데이터 최초 저장 성공");
                    IsDirty = false;
                }
                else
                {
                    Debug.LogError($"족장 데이터 최초 저장 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
                tcs.TrySetResult();
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
                    IsDirty = false;
                }
                else
                {
                    Debug.LogError($"족장 데이터 업데이트 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
                tcs.TrySetResult();
            });
        }
        await tcs.Task;
    }

    public void Load(Action onCompleted = null)
    {
        _loadSucceeded = false;
        Backend.GameData.GetMyData(TABLE_NAME, new Where(), callback =>
        {
            try
            {
                if (callback.IsSuccess())
                {
                    ApplyLoadedRows(callback.FlattenRows());
                    Debug.Log($"족장 데이터 로드 성공: SelectedChiefId={SelectedChiefId}");
                }
                else
                {
                    Debug.LogError($"족장 데이터 로드 실패: {callback.GetStatusCode()} - {callback.GetErrorMessage()}");
                }
            }
            catch (Exception e)
            {
                // Preserve the failing method/line without dumping the player's server response.
                Debug.LogError($"족장 데이터 로드/파싱 실패: {e}");
            }
            finally
            {
                onCompleted?.Invoke();
            }
        });
    }

    private void ApplyLoadedRows(JsonData rows)
    {
        _loadSucceeded = false;
        if (rows == null || !rows.IsArray)
            throw new FormatException("PlayerChiefData rows must be an array.");

        string rowInDate = string.Empty;
        int chiefId = 0;
        if (rows.Count > 0)
        {
            var row = rows[0];
            if (row == null || !row.IsObject)
                throw new FormatException("PlayerChiefData first row must be an object.");
            if (!row.ContainsKey("inDate") || row["inDate"] == null || !row["inDate"].IsString
                || string.IsNullOrWhiteSpace(row["inDate"].ToString()))
                throw new FormatException("PlayerChiefData inDate is missing or invalid; saving is blocked.");
            rowInDate = row["inDate"].ToString();
            if (row.ContainsKey(DATA_KEY) && row[DATA_KEY] != null)
            {
                var value = row[DATA_KEY];
                if (!(value.IsInt || value.IsLong || value.IsString)
                    || !int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out chiefId)
                    || chiefId < 0)
                {
                    chiefId = 0;
                    Debug.LogWarning("저장된 족장 선택 ID 형식이 올바르지 않아 기본 족장을 표시합니다. 서버 데이터는 자동 수정하지 않습니다.");
                }
            }
        }

        // Commit only after the entire row is valid. Loading never writes defaults back to the server.
        _rowInDate = rowInDate;
        IsDirty = false;
        _loadSucceeded = true;
        SelectedChiefId = chiefId;
    }

    private void NotifySelectionChanged()
    {
        var handlers = ChangeSelectId;
        if (handlers == null) return;
        foreach (Action<int> handler in handlers.GetInvocationList())
        {
            try { handler(selectedChiefId); }
            catch (Exception error)
            {
                Debug.LogError($"족장 선택 알림 처리 실패 ({handler.Method.DeclaringType?.Name}.{handler.Method.Name}): {error}");
            }
        }
    }
}
