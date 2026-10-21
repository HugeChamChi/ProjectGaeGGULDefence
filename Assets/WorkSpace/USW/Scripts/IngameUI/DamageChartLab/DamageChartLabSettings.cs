using UnityEngine;

/// <summary>딜표 실험실의 예시 전투와 갱신 주기 설정.</summary>
[CreateAssetMenu(menuName = "USW/UI Lab/Damage Chart Settings")]
public sealed class DamageChartLabSettings : ScriptableObject
{
    /// <summary>종류별 예시 유닛.</summary>
    public DamageChartSample[] Samples;
    /// <summary>최근 DPS 집계 창(전투 초).</summary>
    [Min(.1f)] public float RecentDpsWindow = 5f;
    /// <summary>예시 공격 묶음 간격.</summary>
    [Min(.02f)] public float HitInterval = .22f;
    /// <summary>화면 데이터 갱신 간격.</summary>
    [Min(.05f)] public float RefreshInterval = .16f;
    /// <summary>첫 화면을 준비하는 예시 전투 시간.</summary>
    [Min(0f)] public float WarmupSeconds = 12f;
    /// <summary>순위 역전을 보여 주기 위한 추가 예시 피해량.</summary>
    [Min(1f)] public float BurstDamage = 42000f;
    /// <summary>실제 딜표 패널의 화면 기준 최소 앵커.</summary>
    public Vector2 RuntimePanelMin = new Vector2(.03f, .18f);
    /// <summary>실제 딜표 패널의 화면 기준 최대 앵커.</summary>
    public Vector2 RuntimePanelMax = new Vector2(.97f, .78f);
    /// <summary>스크롤 없이 표시할 행 수.</summary>
    [Min(1)] public int VisibleRows = 6;
    /// <summary>실험실 행 대비 실게임 글자 배율.</summary>
    [Min(.1f)] public float RuntimeTextScale = 1.08f;
    /// <summary>식량·설정 아래 네 버튼 페이지 높이 (기존 HUD Canvas 단위).</summary>
    [Min(100f)] public float HudHeight = 400f;
    /// <summary>작은 HUD에서 초상·막대를 읽을 수 있도록 표시하는 행 수.</summary>
    [Min(1)] public int HudVisibleRows = 3;
    /// <summary>HUD의 피해량 글자 배율.</summary>
    [Min(.1f)] public float HudTextScale = 1.35f;
    /// <summary>좌우 전환 확정에 필요한 화면 폭 비율.</summary>
    [Range(.03f, .4f)] public float HudSwipeFraction = .12f;
    /// <summary>버튼 탭을 가로 드래그로 바꾸기 위해 필요한 HUD 화면 폭 비율.</summary>
    [Range(.005f, .1f)] public float HudButtonSwipeStartFraction = .03f;
    /// <summary>버튼에서 가로 전환을 시작할 때 필요한 가로/세로 이동 거리 비율.</summary>
    [Min(1f)] public float HudButtonSwipeDirectionRatio = 1.5f;
    /// <summary>일시정지 중에도 동작하는 페이지 정착 시간.</summary>
    [Min(.01f)] public float HudSlideSeconds = .22f;
    /// <summary>전환 중 화면 밖으로 나가는 페이지의 최소 배율.</summary>
    [Range(.5f, 1f)] public float HudPageMinScale = .9f;
    /// <summary>전환 중 화면 밖으로 나가는 페이지의 최소 불투명도.</summary>
    [Range(0f, 1f)] public float HudPageMinAlpha = .25f;
    /// <summary>전환이 끝난 페이지가 살짝 튀어 오르는 시작 배율.</summary>
    [Range(1f, 1.2f)] public float HudSettlePopScale = 1.035f;
    /// <summary>정착 튀어 오름이 원래 크기로 돌아오는 시간.</summary>
    [Min(.01f)] public float HudSettlePopSeconds = .25f;
    /// <summary>버튼 페이지의 스와이프 안내.</summary>
    public string HudOpenHint = "‹ 좌우로 밀어서 딜 그래프 보기 ›";
    /// <summary>그래프 페이지의 복귀 안내.</summary>
    public string HudCloseHint = "‹ 좌우로 밀어서 버튼으로 돌아가기 ›";
}
