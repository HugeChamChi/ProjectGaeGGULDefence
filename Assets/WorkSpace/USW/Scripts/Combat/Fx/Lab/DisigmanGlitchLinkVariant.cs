/// <summary>디시그망 스킬 레일건 → 타이머 해킹 글리치 연결 시안 (실험실 FxLab_DisigmanGlitchLink, 2026-10-12).</summary>
public enum DisigmanGlitchLinkVariant
{
    /// <summary>지금 인게임 — 발동 즉시 글리치, 작은 신호점이 유닛에서 출발.</summary>
    Current = 0,
    /// <summary>A — 레일건이 맞는 순간 글리치 시작, 신호점이 착탄점에서 출발.</summary>
    AfterImpact = 1,
    /// <summary>B — 착탄점에서 데이터 조각이 타이머로 빨려 들어가고 도착 순간 글리치.</summary>
    StealShards = 2,
    /// <summary>D — 착탄 순간 보스도 같은 글리치로 찢어지며 타이머와 동시에 깨짐.</summary>
    BossGlitch = 3,
    /// <summary>추천 조합 — 글리치 빔(E) + 보스 글리치(D) + 데이터 조각 회수(B).</summary>
    Combo = 4,
}
