using UnityEngine;

/// <summary>
/// 이펙트 실험실(FxLab_ChiefConvergeBeam 씬) 전용 — 족장 스킬 집결 일제 사격을 반복한다.
///   대기(베탕 주위 호버) → 집결(보스 쪽으로 열린 부채꼴) → 잠깐 숨 고르기 → ChiefConvergeBeamFx 수렴 빔 → 귀환 → 반복.
/// 드론은 실제 DroneUnit 대신 같은 그림의 스프라이트다. 연출 본체(ChiefConvergeBeamFx)는 게임에서도 그대로 쓴다.
/// 빌더: ChiefConvergeBeamLabBuilder (Tools/USW/Fx/Build Chief Converge Beam Lab).
/// </summary>
public sealed class ChiefConvergeBeamLab : MonoBehaviour, IFxLabPlayable
{
    [Header("장면 (빌더가 연결)")]
    [SerializeField] private ChiefConvergeBeamFx _fx;
    [SerializeField] private Transform _target;
    [SerializeField] private SpriteRenderer[] _drones = System.Array.Empty<SpriteRenderer>();
    [SerializeField] private Sprite _idleSprite;
    [SerializeField] private Sprite _fireSprite;

    [Header("집결 대형 (월드)")]
    [SerializeField] private Vector3 _formationCenter = new Vector3(0f, 0f, 0f);
    [Tooltip("부채꼴 반지름 — 드론은 합류점 뒤쪽으로 퍼진다")]
    [SerializeField, Min(0f)] private float _formationRadius = 2.4f;
    [SerializeField, Range(30f, 220f)] private float _arcDegrees = 160f;
    [Tooltip("이 수를 넘으면 안쪽·바깥쪽 두 줄 부채꼴로 나눈다")]
    [SerializeField, Min(1)] private int _singleRowMax = 10;
    [Tooltip("두 줄일 때 안쪽 줄 반지름 비율")]
    [SerializeField, Range(0.3f, 0.95f)] private float _innerRowRatio = 0.68f;
    [Tooltip("실험실에서 쓸 드론 수 (8 / 16 비교). 남는 드론은 숨긴다")]
    [SerializeField, Min(1)] private int _droneCount = 16;

    [Header("시간 (초)")]
    [SerializeField, Min(0.05f)] private float _gather = 0.45f;
    [SerializeField, Min(0f)] private float _windup = 0.1f;
    [SerializeField, Min(0.05f)] private float _return = 0.5f;
    [SerializeField, Min(0f)] private float _rest = 1.2f;
    [SerializeField] private bool _loop = true;
    [SerializeField] private bool _playOnStart = true;

    [Header("드론 움직임")]
    [SerializeField, Min(0f)] private float _hover = 0.06f;
    [Tooltip("발사 순간 뒤로 밀리는 거리")]
    [SerializeField, Min(0f)] private float _recoil = 0.12f;

    private Vector3[] _homes;
    private Transform[] _muzzles;
    private float _t = -1f;
    private bool _launched;
    private float _firedAt = -1f;
    private bool _unscaled;

    /// <inheritdoc/>
    public bool UseUnscaledTime
    {
        get => _unscaled;
        set { _unscaled = value; if (_fx != null) _fx.UseUnscaledTime = value; }
    }

    /// <summary>실험실 메뉴용: 쓸 드론 수를 바꾸고 처음부터 다시 재생한다.</summary>
    public void SetDroneCount(int count)
    {
        _droneCount = Mathf.Clamp(count, 1, _drones.Length);
        RefreshActive();
        Play();
    }

    private int Active => Mathf.Clamp(_droneCount, 1, _drones.Length);

    private void RefreshActive()
    {
        if (_drones == null) return;
        _muzzles = new Transform[Active];
        for (int i = 0; i < _drones.Length; i++)
        {
            bool on = i < Active;
            _drones[i].enabled = on;
            if (on) _muzzles[i] = _drones[i].transform;
        }
    }

    private float Cycle => _gather + _windup + (_fx != null ? _fx.TotalSeconds : 1.5f) + _return + _rest;

    private void Start()
    {
        _homes = new Vector3[_drones.Length];
        for (int i = 0; i < _drones.Length; i++) _homes[i] = _drones[i].transform.position;
        RefreshActive();
        if (_fx != null) _fx.Fired += () => _firedAt = _t;
        if (_playOnStart) Play();
    }

    /// <summary>처음부터 한 번 재생한다 (캡처 시작점).</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        _fx?.Stop();
        _t = 0f;
        _launched = false;
        _firedAt = -1f;
    }

    private void Update()
    {
        if (_t < 0f || _homes == null) return;
        _t += _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        float fxEnd = _gather + _windup + (_fx != null ? _fx.TotalSeconds : 1.5f);

        if (!_launched && _t >= _gather + _windup)
        {
            _launched = true;
            _fx?.Play(_muzzles, _target);
        }

        bool firing = _fx != null && _launched && _fx.IsPlaying && _t - _gather - _windup >= _fx.ChargeSeconds;
        Vector3 dir = (_target.position - _formationCenter).normalized;
        for (int i = 0; i < Active; i++)
        {
            Vector3 slot = Slot(i, dir);
            Vector3 pos;
            if (_t < _gather) pos = Vector3.Lerp(_homes[i], slot, EaseInOut(_t / _gather));
            else if (_t < fxEnd) pos = slot;
            else if (_t < fxEnd + _return) pos = Vector3.Lerp(slot, _homes[i], EaseInOut((_t - fxEnd) / _return));
            else pos = _homes[i];

            if (_firedAt >= 0f && _t < fxEnd)
            {
                float k = Mathf.Exp(-(_t - _firedAt) * 10f);
                pos -= dir * (_recoil * k);
            }
            pos.y += Mathf.Sin((_t * 3f) + i * 0.9f) * _hover;
            _drones[i].transform.position = pos;
            var sprite = firing ? _fireSprite : _idleSprite;
            if (sprite != null && _drones[i].sprite != sprite) _drones[i].sprite = sprite;
        }

        if (_t >= Cycle)
        {
            if (_loop) Play();
            else _t = -1f;
        }
    }

    // 보스 반대쪽으로 열린 부채꼴 — 드론이 합류점 뒤에서 앞으로 빔을 모은다.
    // _singleRowMax를 넘으면 안쪽·바깥쪽 두 줄로 나누고, 바깥 줄은 반 칸 엇갈려 겹치지 않게 한다.
    private Vector3 Slot(int i, Vector3 dir)
    {
        int n = Active;
        bool twoRows = n > _singleRowMax;
        int inner = twoRows ? n / 2 : n;
        int row = twoRows && i >= inner ? 1 : 0;
        int index = row == 0 ? i : i - inner;
        int count = row == 0 ? inner : n - inner;
        float step = count <= 1 ? 0f : 1f / (count - 1);
        float u = count <= 1 ? 0.5f : index * step;
        if (twoRows && row == 1) u = Mathf.Clamp01(u + step * 0.5f - step * 0.25f);
        float a = Mathf.Lerp(-_arcDegrees * 0.5f, _arcDegrees * 0.5f, u);
        float radius = _formationRadius * (twoRows && row == 0 ? _innerRowRatio : 1f);
        Vector3 offset = Quaternion.Euler(0f, 0f, a) * -dir * radius;
        return _formationCenter + offset;
    }

    private static float EaseInOut(float p)
    {
        p = Mathf.Clamp01(p);
        return p * p * (3f - 2f * p);
    }
}
