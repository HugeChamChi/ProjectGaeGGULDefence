using TMPro;
using UnityEngine;

/// <summary>
/// 버프·디버프 상태 팝업(StatusPopupBundle)의 모양과 타이밍 (사용자 확정 A안·주아 글꼴, 2026-10-22).
/// "공격속도 ▲"처럼 문구 + 화살표만 띄우고, 같은 대상에 함께 걸린 효과는 한 묶음으로 줄 세운다.
/// </summary>
[CreateAssetMenu(menuName = "GGD/UI/Status Popup Style", fileName = "StatusPopupStyle")]
public class StatusPopupStyle : ScriptableObject
{
    [Header("글자")]
    public TMP_FontAsset font;
    public float fontSize = 46f;
    [Tooltip("버프 글자 재질(외곽선·그림자 프리셋). 비우면 글꼴 기본 재질")]
    public Material buffMaterial;
    [Tooltip("디버프 글자 재질(외곽선·그림자 프리셋). 비우면 글꼴 기본 재질")]
    public Material debuffMaterial;
    [Tooltip("버프 글자·화살표 색. 노란색은 유닛 몸통 위에서 읽기 어려워 쓰지 않는다 (사용자 피드백 2026-10-22)")]
    public Color buffColor = new Color(0.45f, 0.95f, 0.7f);
    [Tooltip("디버프 글자·화살표 색")]
    public Color debuffColor = new Color(0.788f, 0.635f, 1f);

    [Header("화살표 (글꼴에 ▲▼가 없어 그림으로 붙인다)")]
    [Tooltip("위쪽 삼각형 채움. 디버프는 180도 돌려 쓴다")]
    public Sprite arrowFill;
    [Tooltip("채움보다 두꺼운 같은 삼각형 = 외곽선")]
    public Sprite arrowStroke;
    [Tooltip("글자 외곽선 재질과 같은 어두운 색")]
    public Color arrowStrokeColor = new Color(0.12f, 0.1f, 0.18f);
    public float arrowSize = 34f;
    public float arrowSpacing = 4f;

    [Header("묶음")]
    [Tooltip("한 묶음에 보이는 최대 줄 수. 넘치면 줄은 더하지 않고 유지 시간만 연장")]
    public int maxLines = 3;
    public float lineSpacing = -6f;

    [Header("타이밍")]
    public float enterDuration = 0.26f;
    [Tooltip("등장 통 튀는 정도 (DOTween OutBack overshoot)")]
    public float enterOvershoot = 2.2f;
    [Tooltip("줄이 여러 개일 때 다음 줄이 튀어나오기까지 간격")]
    public float lineStagger = 0.04f;
    [Tooltip("마지막 줄이 다 나온 뒤 머무는 시간")]
    public float holdDuration = 0.5f;
    [Tooltip("위쪽부터 그라데이션으로 흐려지는 시간")]
    public float exitDuration = 0.44f;
    [Tooltip("위로 올라가는 시간. 흐려지는 시간과 따로 둘 수 있다(빨리 올라가고 천천히 흐려짐 등)")]
    public float exitRiseDuration = 0.3f;
    [Tooltip("사라지면서 위로 올라가는 거리(캔버스 단위). 웹 시안 A = 글자 크기의 약 1.3배")]
    public float exitRise = 60f;
    [Tooltip("같은 효과가 다시 걸렸을 때 그 줄이 통 튀는 크기")]
    public float repeatPunch = 0.18f;
    public float repeatPunchDuration = 0.22f;

    [Header("그라데이션 퇴장")]
    [Tooltip("위쪽부터 지워질 때 그라데이션 폭 ÷ 묶음 높이. 웹 시안 A = 약 1 (묶음 전체가 부드럽게 함께 흐려짐), 작을수록 칼로 자르듯 지워짐")]
    public float fadeSoftnessRatio = 0.96f;
}
