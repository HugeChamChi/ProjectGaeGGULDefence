using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 이펙트 실험실(FxLab_DisigmanRailgun 씬) 전용 — 디시그망 임시 레일건을 인게임 리듬으로 반복한다.
/// 일반공격 간격마다 차지 레일건 한 발, _attacksToCharge발을 채우면 다음 차례에 굵은 스킬 한 발.
/// 유닛은 임시 베탕 그림이며 차지·발사 동안 공격 그림으로 바뀌고 발사 순간 반동을 준다.
/// 연출 본체(RailgunBeamFx)는 인게임과 같은 컴포넌트를 템플릿 복제로 쓴다. 빌더: DisigmanRailgunFxLabBuilder.
/// </summary>
public class DisigmanRailgunFxLab : MonoBehaviour, IFxLabPlayable
{
    [Header("장면 (빌더가 연결)")]
    [SerializeField] private SpriteRenderer _unit;
    [SerializeField] private Sprite _idleSprite;
    [SerializeField] private Sprite _fireSprite;
    [SerializeField] private Transform _target;
    [Tooltip("보스 착탄점 무작위 범위 (월드, 반폭)")]
    [SerializeField] private Vector2 _targetSpread = new Vector2(0.6f, 0.4f);
    [Tooltip("유닛 발밑 기준 총구 위치 (월드)")]
    [SerializeField] private Vector2 _muzzleOffset = new Vector2(0f, 0.6f);

    [Header("연출 템플릿 (비활성, 발사마다 복제)")]
    [SerializeField] private RailgunBeamFx _basicFx;
    [SerializeField] private RailgunBeamFx _skillFx;

    [Header("인게임 수치 (디시그망 SO 기준 초안)")]
    [Tooltip("일반공격 간격 (초) — 공속 0.33 ≈ 3.03초")]
    [SerializeField] private float _attackInterval = 3.03f;
    [Tooltip("스킬까지 필요한 일반공격 수")]
    [SerializeField] private int _attacksToCharge = 6;
    [SerializeField] private float _basicCharge = 0.4f;
    [SerializeField] private float _skillCharge = 0.55f;
    [Tooltip("스킬 빔 굵기·섬광 배율")]
    [SerializeField] private float _skillScale = 1.6f;

    [Header("유닛 반동")]
    [SerializeField] private float _recoil = 0.08f;
    [SerializeField] private float _recoilReturn = 0.15f;
    [Tooltip("발사 후 공격 그림 유지 시간")]
    [SerializeField] private float _fireSpriteHold = 0.2f;
    [SerializeField] private bool _playOnStart = true;

    private CancellationTokenSource _cts;
    private bool _unscaled;
    private bool _auto = true;
    private int _charged;
    private Vector3 _unitHome;
    private float _clock, _fireAt = -999f, _spriteUntil = -999f;
    private string _last = "-";

    /// <inheritdoc/>
    public bool UseUnscaledTime { get => _unscaled; set => _unscaled = value; }

    private float Delta => _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

    private void Start()
    {
        if (_unit != null) _unitHome = _unit.transform.position;
        if (_playOnStart) Play();
    }

    private void OnDestroy() => CancelRun();

    /// <summary>충전을 비우고 반복을 처음부터 다시 시작한다.</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        CancelRun();
        _charged = 0;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LoopAsync(_cts.Token).Forget();
    }

    private void CancelRun()
    {
        if (_cts == null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    private async UniTaskVoid LoopAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                if (_auto) Attack();
                await UniTask.Delay((int)(_attackInterval * 1000f), _unscaled, cancellationToken: token);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    // 인게임 순서: 충전이 다 찼으면 이번 차례는 스킬, 아니면 일반공격 후 충전 +1.
    private void Attack()
    {
        if (_charged >= _attacksToCharge)
        {
            _charged = 0;
            Fire(true);
            return;
        }
        Fire(false);
        _charged++;
    }

    private void Fire(bool skill)
    {
        var template = skill ? _skillFx : _basicFx;
        if (template == null || _unit == null || _target == null) return;
        float charge = skill ? _skillCharge : _basicCharge;

        var fx = Instantiate(template);
        fx.gameObject.SetActive(true);
        Vector3 from = _unitHome + (Vector3)_muzzleOffset;
        Vector3 to = _target.position + new Vector3(Random.Range(-_targetSpread.x, _targetSpread.x),
            Random.Range(-_targetSpread.y, _targetSpread.y), 0f);
        fx.Play(transform, from, to, charge, skill ? _skillScale : 1f);

        _fireAt = _clock + charge;
        _spriteUntil = _fireAt + _fireSpriteHold;
        _last = skill ? "SKILL (+0.3s)" : "BASIC";
    }

    private void Update()
    {
        _clock += Delta;
        if (_unit == null) return;

        bool firing = _clock < _spriteUntil;
        var sprite = firing && _fireSprite != null ? _fireSprite : _idleSprite;
        if (sprite != null && _unit.sprite != sprite) _unit.sprite = sprite;

        // 발사 순간 아래로 밀렸다가 돌아온다
        float since = _clock - _fireAt;
        float kick = since >= 0f && since < _recoilReturn ? 1f - since / _recoilReturn : 0f;
        _unit.transform.position = _unitHome + Vector3.down * (_recoil * kick * kick);
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(.6f, Screen.width / 540f);
        var old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float width = Screen.width / scale;
        GUI.Box(new Rect(10, 10, width - 20, 136), "DISIGMAN / RAILGUN (임시)");
        GUI.Label(new Rect(24, 37, width - 48, 24), $"충전 {_charged}/{_attacksToCharge}   마지막: {_last}");
        float buttonWidth = (width - 56f) / 3f;
        if (GUI.Button(new Rect(24, 66, buttonWidth, 34), "일반 발사")) Fire(false);
        if (GUI.Button(new Rect(28 + buttonWidth, 66, buttonWidth, 34), "스킬 발사")) Fire(true);
        if (GUI.Button(new Rect(32 + buttonWidth * 2, 66, buttonWidth, 34), _auto ? "AUTO: ON" : "AUTO: OFF")) _auto = !_auto;
        if (GUI.Button(new Rect(24, 104, buttonWidth, 34), "처음부터")) Play();
        GUI.matrix = old;
    }
}
