using BackEnd;

/// <summary>로그인 상태와 계정 식별자를 제공하는 앱 수명 서비스. 로그인 및 저장 순서는 변경하지 않는다.</summary>
public sealed class BackendSession
{
    /// <summary>SDK의 현재 로그인 상태를 반환한다.</summary>
    public bool IsLoggedIn() => Backend.IsLogin;
    /// <summary>SDK의 현재 닉네임을 반환한다.</summary>
    public string GetNickname() => Backend.UserNickName;
    /// <summary>SDK의 현재 계정 식별자를 반환한다.</summary>
    public string GetUID() => Backend.UID;
}
