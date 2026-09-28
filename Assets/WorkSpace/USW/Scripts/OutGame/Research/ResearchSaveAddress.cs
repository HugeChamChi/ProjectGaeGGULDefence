using System;

/// <summary>요청 생성 시 고정되는 계정/트리 식별자. 늦은 요청도 원래 계정만 대상으로 삼는다.</summary>
public sealed class ResearchSaveAddress
{
    /// <summary>인증 연결 전 로컬 개발 저장 소유자. 뒤끝 게스트 계정을 뜻하지 않는다.</summary>
    public const string LocalAccount = "local";
    /// <summary>인증 공급자가 나중에 제공할 불투명 계정 키.</summary>
    public string AccountKey { get; }
    /// <summary>파일명과 독립적인 트리 키.</summary>
    public string TreeKey { get; }
    /// <summary>무버전 데이터의 원래 키. local에서만 1회 이관한다.</summary>
    public string LegacyKey { get; }
    /// <summary>길이 구분을 사용하여 계정/트리 구분자 충돌을 방지한 PlayerPrefs 키.</summary>
    public string StorageKey => "OutgameUpgrade.Save." + AccountKey.Length + ":" + AccountKey + ":" + TreeKey;
    /// <summary>소유자를 변경할 수 없는 저장 주소를 만든다.</summary>
    public ResearchSaveAddress(string accountKey, string treeKey, string legacyKey = null)
    {
        if (string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(treeKey))
            throw new ArgumentException("계정 키와 TreeKey는 필수입니다.");
        AccountKey = accountKey;
        TreeKey = treeKey;
        LegacyKey = legacyKey;
    }
}
