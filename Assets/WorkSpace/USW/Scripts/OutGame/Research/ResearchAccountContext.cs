using System;

/// <summary>로그인 구현과 분리된 계정 전환 경계. 실제 인증 공급자는 후속 작업에서 연결한다.</summary>
public sealed class ResearchAccountContext
{
    /// <summary>기본값은 local이며 Google/뒤끝 로그인 상태를 추정하지 않는다.</summary>
    public string AccountKey { get; private set; } = ResearchSaveAddress.LocalAccount;
    /// <summary>A→B→A도 서로 다른 화면 요청으로 구별하는 세대 번호.</summary>
    public long Generation { get; private set; }
    /// <summary>계정 교체 후 화면의 진행을 다시 불러오도록 알린다.</summary>
    public event Action OnAccountChanged;
    /// <summary>확인된 계정 키를 공급한다. null/빈 값은 로그인 전 local로 돌아간다.</summary>
    public void SetAccountKey(string accountKey)
    {
        string next = string.IsNullOrWhiteSpace(accountKey) ? ResearchSaveAddress.LocalAccount : accountKey;
        if (next == AccountKey) return;
        AccountKey = next;
        Generation++;
        OnAccountChanged?.Invoke();
    }
}
