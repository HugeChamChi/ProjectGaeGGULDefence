using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 회전 UI 시안 하나의 공통 틀. 실험실이 토템의 화면 위치에 Root를 옮기고, 후보 방향(0 위·1 오른쪽·2 아래·3 왼쪽, -1 취소)과
/// 손가락까지의 거리(캔버스 단위)를 넘겨준다. 시안마다 모양만 다르고 입력 판정은 실험실이 한다.
/// </summary>
public abstract class TotemRotateLabGizmo
{
    /// <summary>방향별 단위 벡터(화면 기준, 위가 +y).</summary>
    protected static readonly Vector2[] Directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    /// <summary>취소 영역이 활성일 때의 색.</summary>
    protected static readonly Color CancelColor = new Color(1f, 0.42f, 0.36f, 1f);
    /// <summary>취소 영역 기본 색.</summary>
    protected static readonly Color DeadZoneColor = new Color(1f, 1f, 1f, 0.75f);

    private readonly CanvasGroup _group;
    private Image _deadZone;
    private Tween _showTween;

    /// <summary>토템 화면 위치를 따라가는 루트.</summary>
    public RectTransform Root { get; }
    /// <summary>이 시안의 취소 반경(캔버스 단위). 0 이하면 실험실이 기기 픽셀 기준 원본 값을 쓴다.</summary>
    public abstract float DeadZone { get; }
    /// <summary>시안 이름.</summary>
    public abstract string Title { get; }
    /// <summary>시안 설명(실험실 패널 표시).</summary>
    public abstract string Description { get; }
    /// <summary>0보다 크면 이 반경(캔버스 단위) 밖으로 끌었을 때 취소(명일방주 규칙). 0이면 바깥 취소 없음.</summary>
    public virtual float CancelBeyond => 0f;
    /// <summary>방향을 아직 안 골랐을 때의 안내 문구.</summary>
    public virtual string IdleHint => "방향으로 끌기 · 가운데에서 놓으면 <color=#FF7A6B>취소</color>";
    /// <summary>"놓으면 ~쪽으로 회전" 힌트를 띄울지.</summary>
    public virtual bool ShowsHint => true;
    /// <summary>나타날 때 튕기며 커지는지. 원본 재현은 즉시 표시.</summary>
    protected virtual bool AnimatesIn => true;
    /// <summary>현재 후보 방향.</summary>
    protected int Candidate { get; private set; } = -1;
    /// <summary>공용 도형 스프라이트.</summary>
    protected TotemRotateLabSprites Sprites { get; }

    /// <summary>캔버스 아래에 비활성 루트를 만든다.</summary>
    protected TotemRotateLabGizmo(RectTransform parent, TotemRotateLabSprites sprites, string name)
    {
        Sprites = sprites;
        Root = NewRect(name, parent, Vector2.zero);
        _group = Root.gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        Root.gameObject.SetActive(false);
    }

    /// <summary>토템 범위 모양이 바뀌었을 때(미니 범위 아이콘용). 기본은 무시.</summary>
    public virtual void SetShape(IReadOnlyList<Vector2Int> effect, IReadOnlyList<Vector2Int> disabled) { }

    /// <summary>회전 모드 진입.</summary>
    public void Show()
    {
        Root.gameObject.SetActive(true);
        _showTween?.Kill();
        Candidate = -1;
        OnCandidateChanged(-1);
        if (AnimatesIn)
        {
            Root.localScale = Vector3.one * 0.7f;
            _group.alpha = 0f;
            _showTween = DOTween.Sequence().SetUpdate(true)
                .Join(Root.DOScale(1f, 0.26f).SetEase(Ease.OutBack, 2.2f))
                .Join(_group.DOFade(1f, 0.12f));
        }
        else
        {
            Root.localScale = Vector3.one;
            _group.alpha = 1f;
        }
        OnShow();
    }

    /// <summary>회전 모드 종료.</summary>
    public void Hide()
    {
        _showTween?.Kill();
        Root.gameObject.SetActive(false);
        OnHide();
    }

    /// <summary>후보 방향 갱신. 바뀔 때만 시안 연출을 부른다.</summary>
    public void SetCandidate(int candidate)
    {
        if (candidate == Candidate) return;
        Candidate = candidate;
        if (_deadZone != null) _deadZone.color = candidate < 0 ? CancelColor : DeadZoneColor;
        OnCandidateChanged(candidate);
    }

    /// <summary>매 프레임 토템 중심에서 손가락까지의 거리(캔버스 단위).</summary>
    public virtual void SetPointer(Vector2 delta) { }

    /// <summary>매 프레임(실제 시간).</summary>
    public virtual void Tick(float unscaledTime) { }

    /// <summary>정리.</summary>
    public virtual void Dispose()
    {
        _showTween?.Kill();
        if (Root != null) Object.Destroy(Root.gameObject);
    }

    /// <summary>후보가 바뀌었을 때의 시안별 연출.</summary>
    protected abstract void OnCandidateChanged(int candidate);
    /// <summary>표시 직후.</summary>
    protected virtual void OnShow() { }
    /// <summary>숨긴 직후.</summary>
    protected virtual void OnHide() { }

    /// <summary>취소 영역 점선 원을 만든다(새 시안 공통).</summary>
    protected Image AddDeadZone(float radius)
    {
        _deadZone = AddImage("DeadZone", Root, Sprites.DashedRing, Vector2.one * radius * 2f, DeadZoneColor);
        return _deadZone;
    }

    /// <summary>가운데 빨간 ×(취소 상태 표시). 처음엔 꺼져 있다.</summary>
    protected GameObject AddCancelCross()
    {
        var cross = NewRect("Cancel", Root, Vector2.zero).gameObject;
        for (int i = 0; i < 2; i++)
        {
            var bar = AddImage("Bar_" + i, cross.transform, Sprites.RoundRect, new Vector2(12f, 52f), CancelColor);
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
        }
        cross.SetActive(false);
        return cross;
    }

    /// <summary>위(12시)에서 시작해 시계 방향으로 fill만큼 채우는 링 이미지. 가운데가 위를 보도록 돌려 둔다.</summary>
    protected static Image AddArc(string name, Transform parent, Sprite ring, float diameter, float fill, Color color)
    {
        var arc = AddImage(name, parent, ring, Vector2.one * diameter, color);
        arc.type = Image.Type.Filled;
        arc.fillMethod = Image.FillMethod.Radial360;
        arc.fillOrigin = (int)Image.Origin360.Top;
        arc.fillClockwise = true;
        arc.fillAmount = fill;
        arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, fill * 180f);
        return arc;
    }

    /// <summary>가운데 기준 RectTransform.</summary>
    protected static RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>레이캐스트 없는 이미지.</summary>
    protected static Image AddImage(string name, Transform parent, Sprite sprite, Vector2 size, Color color)
    {
        var image = NewRect(name, parent, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    /// <summary>방향 인덱스 → 이 방향을 가리키는 Z 회전(위=0, 시계 방향 음수).</summary>
    protected static float AngleOf(int direction) => -90f * direction;
}
