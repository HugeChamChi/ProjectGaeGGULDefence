using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Coupon request/result journal and account receipt persistence; the backend validates the code.</summary>
public sealed class CouponManager
{
    [VContainer.Inject] private BackendSession _session;
    private bool _busy;
    private const string JournalPrefix = "Coupon.Pending.v1.";
    /// <summary>Current request status.</summary>
    public bool IsBusy => _busy;
    /// <summary>Compatibility callback boundary for existing callers.</summary>
    public void UseCoupon(string code, Action<bool, string> callback = null) => UseWithCallbackAsync(code, callback).Forget();
    private async UniTaskVoid UseWithCallbackAsync(string code, Action<bool, string> callback)
    { var result = await RedeemAsync(code); callback?.Invoke(result.Success, result.Message); }

    /// <summary>Validates input, prevents overlapping requests and confirms reward saving before reporting completion.</summary>
    public async UniTask<(bool Success, string Message)> RedeemAsync(string code)
    {
        code = code?.Trim();
        if (string.IsNullOrEmpty(code)) return (false, "쿠폰 코드를 입력해 주세요.");
        if (code.Length > 256 || ContainsControl(code)) return (false, "쿠폰 코드 형식을 확인해 주세요.");
        if (_busy) return (false, "쿠폰을 처리하고 있습니다.");
        if (_session?.IsLoggedIn() != true || !Player.PlayerData.IsReady) return (false, "로그인과 재화 조회를 완료한 뒤 다시 시도해 주세요.");
        _busy = true;
        string owner = Backend.UserInDate;
        string key = JournalPrefix + owner;
        try
        {
            if (PlayerPrefs.HasKey(key)) return await RecoverAsync(owner, key);
            if (Player.PlayerData.Data.CouponHistory?.Exists(item => item.CodeFingerprint == Fingerprint(code)) == true)
                return (false, "이 계정에서 이미 사용한 쿠폰입니다.");
            if (!await Player.PlayerData.TrySaveAsync()) return (false, "기존 재화 저장을 완료한 뒤 다시 시도해 주세요.");
            if (Backend.UserInDate != owner) return (false, "계정이 변경되었습니다.");
            var journal = new CouponRedemptionJournal { Fingerprint = Fingerprint(code) };
            Persist(key, journal);
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
            var completion = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.Coupon.UseCoupon(code, response =>
            {
                // Persist even if the waiting UI timed out or the user switched accounts.
                if (response.IsSuccess()) { journal.Response = response.GetReturnValuetoJSON().ToJson(); Persist(key, journal); }
                else if (response.StatusCode >= 400 && response.StatusCode < 500 && response.StatusCode != 408 && response.StatusCode != 429)
                { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
                completion.TrySetResult(response);
            });
            var response = await completion.Task.AttachExternalCancellation(timeout.Token);
            if (Backend.UserInDate != owner) return (false, "계정이 변경되었습니다. 원래 계정에서 지급 상태를 확인해 주세요.");
            if (!response.IsSuccess()) return (false, FailureMessage(response.StatusCode, response.GetMessage()));
            return await RecoverAsync(owner, key);
        }
        catch (Exception error)
        { Debug.LogWarning("[Coupon] " + error.GetType().Name); return (false, "사용 결과를 확인하지 못했습니다. 이력을 확인한 뒤 고객지원에 문의해 주세요. 자동으로 다시 사용하지 않습니다."); }
        finally { _busy = false; }
    }
    /// <summary>Replays a captured response only; never submits the coupon again.</summary>
    public async UniTask<(bool Success, string Message)> RecoverPendingAsync()
    {
        if (_busy || !Player.PlayerData.IsReady) return (false, "계정 데이터를 불러오는 중입니다.");
        string owner = Backend.UserInDate;
        string key = JournalPrefix + owner;
        if (!PlayerPrefs.HasKey(key)) return (true, "");
        _busy = true;
        try { return await RecoverAsync(owner, key); }
        catch (Exception) { return (false, "보관된 쿠폰 결과 확인이 필요합니다. 고객지원에 문의해 주세요."); }
        finally { _busy = false; }
    }
    private static async UniTask<(bool Success, string Message)> RecoverAsync(string owner, string key)
    {
        var journal = JsonUtility.FromJson<CouponRedemptionJournal>(PlayerPrefs.GetString(key));
        if (journal == null || string.IsNullOrEmpty(journal.Response)) return (false, "쿠폰 사용 결과 확인이 필요합니다. 회원코드와 쿠폰을 고객지원에 전달해 주세요.");
        if (!CouponRewardParser.TryParse(journal.Response, out var receipt, out string error)) return (false, error + " 응답은 보관되었습니다.");
        receipt.CodeFingerprint = journal.Fingerprint;
        if (Backend.UserInDate != owner || !Player.PlayerData.TryApplyCoupon(receipt)) return (false, "보상 지급을 보류했습니다. 계정/보유량을 확인해 주세요.");
        if (!await Player.PlayerData.TrySaveAsync()) return (false, "쿠폰은 사용되었습니다. 보상 저장 대기 중이며 다시 누르면 저장만 재시도합니다.");
        if (Backend.UserInDate != owner) return (false, "원래 계정에서 지급 상태를 확인해 주세요.");
        PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
        return (true, $"쿠폰 지급 완료 · Gold +{receipt.Gold}, Dia +{receipt.Diamond}, 스태미나 +{receipt.Stamina}");
    }
    /// <summary>Backend 404 combines expiry/invalid/used cases; do not invent a more precise reason.</summary>
    public static string FailureMessage(int status, string message)
    {
        if (status == 404 && message != null && message.Contains("이미 사용하신")) return "이 계정에서 이미 사용한 쿠폰입니다.";
        if (status == 404 && message != null && message.Contains("전부 사용")) return "사용 가능한 수량이 모두 소진된 쿠폰입니다.";
        return status switch
        {
            400 => "쿠폰 입력값 또는 사용 조건을 확인해 주세요.",
            401 or 403 => "로그인 상태 또는 쿠폰 사용 권한을 확인해 주세요.",
            404 => "사용할 수 없는 쿠폰입니다. 코드·사용 기간·사용 여부를 확인해 주세요.",
            409 => "이미 사용했거나 중복 요청된 쿠폰입니다.",
            429 => "요청이 많습니다. 잠시 후 사용 결과를 확인해 주세요.",
            _ => "서버에서 사용 결과를 확인하지 못했습니다. 잠시 후 지급 상태를 확인해 주세요."
        };
    }
    private static bool ContainsControl(string code) { foreach (char c in code) if (char.IsControl(c)) return true; return false; }
    private static string Fingerprint(string code) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(code))).Replace("-", ""); }
    private static void Persist(string key, CouponRedemptionJournal journal) { PlayerPrefs.SetString(key, JsonUtility.ToJson(journal)); PlayerPrefs.Save(); }
}
