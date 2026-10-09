using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 떠 있는 알림 한 개 — 상태(문구·나이·횟수)와 화면 부품. CenterToast가 풀로 재사용하고,
/// ICenterToastMotion이 매 프레임 부품을 배치한다 (방식마다 쓰는 부품만 켠다).
/// 그리는 순서(뒤→앞): Glow, Rim, Background, LineA, LineB, Icon, Clip(Sweep, Text), Badge.
/// </summary>
public sealed class CenterToastEntry
{
    /// <summary>Clip을 안 쓸 때 크기 (사실상 자르지 않음).</summary>
    public static readonly Vector2 NoClipSize = new Vector2(4000f, 600f);

    /// <summary>표시 문구.</summary>
    public string Message;
    public CenterToastKind Kind;
    /// <summary>같은 문구가 합쳐진 횟수 (합치는 방식만, 처음 1).</summary>
    public int Count;
    /// <summary>뜬 뒤 지난 초.</summary>
    public float Age;
    /// <summary>이 나이가 되면 사라지기 시작한다.</summary>
    public float ExitAt;
    /// <summary>마지막으로 다시 눌린 뒤 지난 초 (없으면 무한대).</summary>
    public float BumpAge = float.PositiveInfinity;
    /// <summary>쌓인 순서 (0 = 가장 새 것, 부드럽게 따라감).</summary>
    public float Slot;
    /// <summary>새 알림에 밀려 빨리 사라지는 중.</summary>
    public bool Kicked;
    /// <summary>문구 폭 (자간 0 기준).</summary>
    public float TextWidth;
    /// <summary>방식이 Prepare에서 정하는 전체 폭 (배치 계산용).</summary>
    public float Width;
    /// <summary>방식이 Prepare에서 정하는 바탕 높이 (라인 패널 계열).</summary>
    public float Height;

    public RectTransform Root;
    public CanvasGroup Group;
    public Image Glow;
    public Image Rim;
    public Image Background;
    public Image LineA;
    public Image LineB;
    public Image Icon;
    public TextMeshProUGUI IconText;
    /// <summary>글자·빛 스윕을 잘라 보이는 영역 (RectMask2D).</summary>
    public RectTransform Clip;
    public Image Sweep;
    public TextMeshProUGUI Text;
    public Image Badge;
    public TextMeshProUGUI BadgeText;

    /// <summary>사라지는 중인지.</summary>
    public bool Exiting => Age >= ExitAt;

    /// <summary>사라짐 진행도 0~1.</summary>
    public float ExitProgress(float exitSeconds) => Exiting ? Mathf.Clamp01((Age - ExitAt) / Mathf.Max(0.0001f, exitSeconds)) : 0f;

    /// <summary>모든 부품을 끄고 기본값으로 (방식이 필요한 것만 다시 켠다).</summary>
    public void HideParts()
    {
        Glow.enabled = false;
        Rim.enabled = false;
        Background.enabled = false;
        LineA.enabled = false;
        LineB.enabled = false;
        Icon.enabled = false;
        IconText.enabled = false;
        Sweep.enabled = false;
        Badge.enabled = false;
        BadgeText.enabled = false;
        Clip.sizeDelta = NoClipSize;
        Clip.anchoredPosition = Vector2.zero;
        Clip.localScale = Vector3.one;
        Text.rectTransform.anchoredPosition = Vector2.zero;
        Text.rectTransform.localScale = Vector3.one;
        Text.maxVisibleCharacters = 99999;
        Text.characterSpacing = 0f;
        foreach (var img in new[] { Glow, Rim, Background, LineA, LineB, Icon, Sweep, Badge })
        {
            img.rectTransform.localScale = Vector3.one;
            img.rectTransform.localRotation = Quaternion.identity;
            img.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
