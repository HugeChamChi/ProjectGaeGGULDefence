using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>UID-bound web coupon launch and return flow. Registration and mail dispatch belong to BACKND.</summary>
public sealed class WebCouponService
{
    private readonly WebCouponConfig _config;
    private readonly WebCouponGateway _gateway;
    private string _openedOwner, _openedUid;
    private float _lastLaunch = float.NegativeInfinity;
    private bool _awaitingReturn, _refreshing;
    private const float LaunchCooldownSeconds = 2;
    /// <summary>Creates the app service from authored configuration and platform boundary.</summary>
    public WebCouponService(WebCouponConfig config, WebCouponGateway gateway) { _config = config; _gateway = gateway; }
    /// <summary>Current public account code.</summary>
    public string Uid => IsAuthenticated ? _gateway.Uid : "";
    /// <summary>Current account display name.</summary>
    public string Nickname => IsAuthenticated ? _gateway.Nickname : "";
    /// <summary>Prevents opening under a stale or missing account.</summary>
    public bool IsAuthenticated => _gateway.LoggedIn && !string.IsNullOrEmpty(_gateway.Owner) && !string.IsNullOrEmpty(_gateway.Uid);
    /// <summary>True while a pending-mail refresh is in progress.</summary>
    public bool IsBusy => _refreshing || _gateway.MailBusy;

    /// <summary>Validates the official page URL and replaces any previous UID, preserving language.</summary>
    public static bool TryBuildUrl(string pageUrl, string uid, out string url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(uid)) return false;
        try
        {
            if (!Uri.TryCreate(pageUrl?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || !string.Equals(uri.Host, "storage.thebackend.io", StringComparison.OrdinalIgnoreCase)
                || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !uri.AbsolutePath.EndsWith("/coupon.html", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(uri.Fragment)) return false;
            var query = new List<string>();
            foreach (string pair in uri.Query.TrimStart('?').Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                string key = Uri.UnescapeDataString(pair.Split('=')[0]);
                if (!string.Equals(key, "uid", StringComparison.OrdinalIgnoreCase)) query.Add(pair);
            }
            query.Add("uid=" + Uri.EscapeDataString(uid));
            url = new UriBuilder(uri) { Query = string.Join("&", query) }.Uri.AbsoluteUri;
            return true;
        }
        catch (Exception) { return false; }
    }
    /// <summary>Opens the browser exactly once per tap interval; never submits a coupon or reports success.</summary>
    public bool TryOpen(out string message)
    {
        if (!IsAuthenticated) { message = "로그인한 뒤 쿠폰을 등록해 주세요."; return false; }
        if (_gateway.Now - _lastLaunch < LaunchCooldownSeconds) { message = "쿠폰 페이지를 여는 중입니다."; return false; }
        if (_config == null || !TryBuildUrl(_config.PageUrl, _gateway.Uid, out string url))
        { message = "쿠폰 페이지를 준비 중입니다. 잠시 후 다시 시도해 주세요."; return false; }
        string owner = _gateway.Owner, uid = _gateway.Uid;
        try { _gateway.OpenUrl(url); }
        catch (Exception) { message = "브라우저를 열지 못했습니다. 다시 시도해 주세요."; return false; }
        _lastLaunch = _gateway.Now; _openedOwner = owner; _openedUid = uid; _awaitingReturn = true;
        message = "웹페이지에서 등록 결과를 확인해 주세요. 등록에 성공하면 우편함에서 선물을 받을 수 있습니다.";
        return true;
    }
    /// <summary>Refreshes on browser return only for the account that opened the page; duplicate focus/pause events coalesce.</summary>
    public async UniTask<string> ReturnAsync(CancellationToken token)
    {
        if (!_awaitingReturn) return null;
        if (!IsAuthenticated || _openedOwner != _gateway.Owner || _openedUid != _gateway.Uid)
        { _awaitingReturn = false; return "계정이 변경되었습니다. 현재 계정으로 쿠폰 페이지를 다시 열어 주세요."; }
        if (IsBusy) return "우편을 확인하고 있습니다. 잠시 기다려 주세요.";
        _awaitingReturn = false;
        return await RefreshAsync(token);
    }
    /// <summary>Loads pending mail and recovers previously captured legacy receipts; never submits a coupon.</summary>
    public async UniTask<string> RefreshAsync(CancellationToken token)
    {
        if (!IsAuthenticated) return "로그인한 뒤 우편을 확인해 주세요.";
        if (IsBusy) return "우편을 확인하고 있습니다. 잠시 후 다시 시도해 주세요.";
        _refreshing = true; string owner = _gateway.Owner;
        try
        {
            string legacy = await _gateway.RecoverLegacyAsync().AttachExternalCancellation(token);
            if (owner != _gateway.Owner || !IsAuthenticated) return "계정이 변경되었습니다. 현재 계정의 우편함을 확인해 주세요.";
            await _gateway.ReloadMailAsync().AttachExternalCancellation(token);
            if (owner != _gateway.Owner || !IsAuthenticated) return "계정이 변경되었습니다. 현재 계정의 우편함을 확인해 주세요.";
            string status = string.IsNullOrEmpty(_gateway.MailStatus) ? "쿠폰 우편을 확인했습니다. 웹페이지의 등록 결과도 함께 확인해 주세요." : _gateway.MailStatus;
            return string.IsNullOrEmpty(legacy) ? status : "이전 쿠폰 처리: " + legacy + "\n" + status;
        }
        catch (OperationCanceledException) { return "우편 확인이 중단되었습니다. 우편함을 열면 다시 확인합니다."; }
        catch (Exception) { return "우편을 확인하지 못했습니다. 우편함을 열면 다시 확인합니다."; }
        finally { _refreshing = false; }
    }
}
