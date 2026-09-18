using DG.Tweening;
using UnityEngine;

/// <summary>
/// 족장 액티브 스킬의 재사용 대기시간이 다 차면(CanActivate: false → true) 지정한 배너를
/// 화면 밖 왼쪽에서 제자리로 슬라이드시켜 알린다. 스킬을 실제로 사용하면(다시 false)
/// 같은 경로로 화면 밖으로 되돌릴 수 있다(<see cref="_hideOnUse"/>).
/// </summary>
public class ChiefSkillReadyBanner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("슬라이드할 배너 RectTransform (원래 위치가 도착 지점이 된다)")]
    [SerializeField] private RectTransform _bannerRect;

    [Tooltip("족장 액티브 스킬 상태를 가져올 대상. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private ChieftainSpawner _chieftainSpawner;

    [Header("Slide")]
    [Tooltip("등장 시작 지점의 화면 밖 X 오프셋 (음수 = 왼쪽)")]
    [SerializeField] private float _offScreenOffsetX = -1200f;
    [SerializeField] private float _inDuration = 0.4f;
    [SerializeField] private Ease _inEase = Ease.OutBack;
    [SerializeField] private float _outDuration = 0.3f;
    [SerializeField] private Ease _outEase = Ease.InBack;

    [Tooltip("스킬을 실제로 사용하면 배너를 다시 화면 밖으로 되돌릴지 여부")]
    [SerializeField] private bool _hideOnUse = true;

    private IChiefActiveSkill _skill;
    private Vector2 _restingPos;
    private bool _wasReady;
    private Tween _slideTween;

    private void Awake()
    {
        if (_bannerRect != null)
            _restingPos = _bannerRect.anchoredPosition;

        if (_chieftainSpawner == null)
            _chieftainSpawner = Object.FindFirstObjectByType<ChieftainSpawner>();
    }

    private void OnEnable()
    {
        if (_chieftainSpawner == null) return;

        _chieftainSpawner.OnActiveSkillChanged += HandleActiveSkillChanged;
        HandleActiveSkillChanged(_chieftainSpawner.ActiveSkill);
    }

    private void OnDisable()
    {
        if (_chieftainSpawner != null)
            _chieftainSpawner.OnActiveSkillChanged -= HandleActiveSkillChanged;

        _slideTween?.Kill();
    }

    private void HandleActiveSkillChanged(IChiefActiveSkill skill)
    {
        _skill = skill;
        _wasReady = false;
        SnapOffScreen();
    }

    private void Update()
    {
        if (_skill == null || !_skill.IsAvailable || _bannerRect == null) return;

        bool isReady = _skill.CanActivate;
        if (isReady == _wasReady) return;
        _wasReady = isReady;

        if (isReady)
            SlideIn();
        else if (_hideOnUse)
            SlideOut();
    }

    private void SnapOffScreen()
    {
        if (_bannerRect == null) return;
        _slideTween?.Kill();
        _bannerRect.anchoredPosition = _restingPos + new Vector2(_offScreenOffsetX, 0f);
    }

    private void SlideIn()
    {
        if (_bannerRect == null) return;
        _slideTween?.Kill();
        _bannerRect.anchoredPosition = _restingPos + new Vector2(_offScreenOffsetX, 0f);
        _slideTween = _bannerRect.DOAnchorPos(_restingPos, _inDuration).SetEase(_inEase);
    }

    private void SlideOut()
    {
        if (_bannerRect == null) return;
        _slideTween?.Kill();
        _slideTween = _bannerRect.DOAnchorPos(_restingPos + new Vector2(_offScreenOffsetX, 0f), _outDuration).SetEase(_outEase);
    }
}
