using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>Free nickname changes; validates input and serializes requests per popup scope.</summary>
public sealed class ProfileNicknameService
{
    private const int RequestTimeoutSeconds = 15;
    private readonly ProfileNicknameGateway _gateway;
    private readonly ProfilePopupConfig _config;
    private bool _busy;
    /// <summary>Uses an explicit account boundary and authored rules.</summary>
    public ProfileNicknameService(ProfileNicknameGateway gateway, ProfilePopupConfig config)
    { _gateway = gateway; _config = config; }
    /// <summary>True while either the caller or a timed-out SDK mutation is still running.</summary>
    public bool IsBusy => _busy || _gateway.IsChanging;
    /// <summary>Displayed input guidance, with no currency cost.</summary>
    public string Guide => $"{_config.MinimumNicknameLength}~{_config.MaximumNicknameLength}자로 입력해 주세요.";

    /// <summary>Returns an empty string for valid names; preserves intentional internal spaces.</summary>
    public string Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim()) return "닉네임 앞뒤에 공백을 넣을 수 없어요.";
        foreach (char value in name) if (char.IsControl(value)) return "사용할 수 없는 문자가 있어요.";
        int length = new StringInfo(name).LengthInTextElements;
        return length < _config.MinimumNicknameLength || length > _config.MaximumNicknameLength ? Guide : "";
    }

    /// <summary>Confirms the backend result before reporting success; rejects duplicate submissions.</summary>
    public async UniTask<string> ChangeAsync(string name, CancellationToken token)
    {
        if (IsBusy) return "변경 결과를 확인하고 있어요. 잠시 기다려 주세요.";
        string invalid = Validate(name); if (invalid.Length > 0) return invalid;
        if (!_gateway.IsReady) return "계정 정보를 먼저 불러와 주세요.";
        if (name == _gateway.Nickname) return "현재 닉네임과 같아요.";
        token.ThrowIfCancellationRequested();
        _busy = true; string owner = _gateway.Owner;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(RequestTimeoutSeconds), DelayType.Realtime);
        try
        {
            int status = await _gateway.ChangeAsync(name, timeout.Token);
            if (owner != _gateway.Owner) return "계정이 변경되었어요.";
            if (status >= 200 && status < 300)
            {
                bool saved = await _gateway.SaveAsync();
                if (owner != _gateway.Owner) return "계정이 변경되었어요.";
                return saved ? "닉네임을 변경했어요." : "닉네임을 변경했어요. 표시 정보 저장은 재시도 중이에요.";
            }
            return status == 409 ? "이미 사용 중인 닉네임이에요." : status == 400 ? "사용할 수 없는 닉네임이에요." : "변경하지 못했어요. 잠시 후 다시 시도해 주세요.";
        }
        catch (OperationCanceledException) { return "변경 결과를 확인 중이에요. 잠시 후 프로필을 다시 열어 주세요."; }
        catch (Exception) { return "변경 결과를 확인하지 못했어요. 프로필을 다시 열어 주세요."; }
        finally { _busy = false; }
    }

    /// <summary>Loads the authoritative nickname without a mutation.</summary>
    public async UniTask RefreshAsync(CancellationToken token)
    {
        if (IsBusy) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(RequestTimeoutSeconds), DelayType.Realtime);
        try { await _gateway.RefreshAsync(timeout.Token); }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }
}
