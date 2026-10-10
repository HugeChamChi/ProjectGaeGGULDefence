using TMPro;
using UnityEngine;

/// <summary>hack_gauge_E.zip의 사선 스트립 아트·폰트 참조. 런타임 파일 로딩 없이 씬에서 주입한다.</summary>
[CreateAssetMenu(menuName = "GaeGGUL/UI/Hack Slanted Gauge Style")]
public sealed class HackSlantedGaugeStyle : ScriptableObject
{
    /// <summary>스트립 배경.</summary>
    public Sprite Background;
    /// <summary>50 미만 HACK 태그.</summary>
    public Sprite HackTag;
    /// <summary>50 도달 READY 태그.</summary>
    public Sprite ReadyTag;
    /// <summary>2스택을 표시하는 눈금.</summary>
    public Sprite TickOn;
    /// <summary>홀수의 남은 1스택을 표시하는 눈금.</summary>
    public Sprite TickHalf;
    /// <summary>빈 눈금.</summary>
    public Sprite TickOff;
    /// <summary>Chakra Petch Bold 숫자 폰트.</summary>
    public TMP_FontAsset Font;
    /// <summary>숫자 외곽선이 설정된 공유 재질.</summary>
    public Material FontMaterial;

    /// <summary>뷰를 생성할 모든 참조가 준비되어 있는지.</summary>
    public bool IsComplete => Background != null && HackTag != null && ReadyTag != null
        && TickOn != null && TickHalf != null && TickOff != null && Font != null && FontMaterial != null;
}
