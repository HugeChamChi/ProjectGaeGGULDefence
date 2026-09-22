using GaeGGUL.Animation;
using UnityEngine;

/// <summary>유닛 단위의 스턴 시간과 호흡/VFX를 관리한다. 공격 경로는 이 상태를 참조한다.</summary>
[DefaultExecutionOrder(-50)]
public sealed class UnitStunState : MonoBehaviour
{
    private UnitBase _unit;
    private DragHandler _dragHandler;
    private Anim_Breathing[] _breathing;
    private StunStarsVisual _visual;
    private float _remaining;
    /// <summary>남은 게임 시간. 중복은 더하지 않고 긴 시간을 사용한다.</summary>
    public float Remaining => _remaining;
    /// <summary>현재 스턴 상태인지 여부.</summary>
    public bool IsStunned => _remaining > 0f;

    private void Awake()
    {
        _unit = GetComponent<UnitBase>();
        _dragHandler = GetComponent<DragHandler>();
        _breathing = GetComponentsInChildren<Anim_Breathing>(true);
    }

    /// <summary>배치된 유닛에 스턴을 적용한다. VFX 실패는 상태 적용을 취소하지 않는다.</summary>
    public void Apply(float seconds, StunVisualSettings visual)
    {
        if (_unit == null || !_unit.isActiveAndEnabled || _unit.currentCell == null || _unit.StunImmune || seconds <= 0f) return;
        _remaining = Mathf.Max(_remaining, seconds);
        _dragHandler?.CancelPointerDrag();
        foreach (var breath in _breathing) if (breath != null) breath.SetStatusPaused(true);
        if (_visual != null || visual == null || visual.Material == null) return;
        try
        {
            _visual = new GameObject("Stun Stars").AddComponent<StunStarsVisual>();
            _visual.Initialize(_unit, visual);
        }
        catch (System.Exception error)
        {
            if (_visual != null) Destroy(_visual.gameObject);
            _visual = null;
            Debug.LogException(error, this);
        }
    }

    private void Update()
    {
        if (!IsStunned || Time.timeScale <= 0f) return;
        _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
        if (_remaining <= 0f) Clear();
    }

    /// <summary>제거/풀 반환 시 상태와 연출을 함께 정리한다.</summary>
    public void Clear()
    {
        _remaining = 0f;
        if (_breathing != null)
            foreach (var breath in _breathing) if (breath != null) breath.SetStatusPaused(false);
        if (_visual != null) { _visual.gameObject.SetActive(false); Destroy(_visual.gameObject); }
        _visual = null;
    }

    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
