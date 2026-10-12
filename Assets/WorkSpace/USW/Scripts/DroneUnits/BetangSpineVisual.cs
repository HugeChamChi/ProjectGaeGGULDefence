using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>베탕의 등급별 팔 전개와 스킬 모션을 재생하고 저작된 Spine 발사 신호를 유닛에 전달한다.</summary>
[DefaultExecutionOrder(200)]
public sealed class BetangSpineVisual : MonoBehaviour, IPauseIdleVisual
{
    [SerializeField] private SkeletonAnimation _skeleton;
    [SerializeField] private SpriteRenderer _source;
    [SerializeField] private DragHandler _drag;
    [SerializeField] private string _idle = "idle";
    [SerializeField] private string _skill = "skill";
    [SerializeField] private SpineSkillLaunchEvent _skillLaunchEvent;
    [SerializeField, Min(0f)] private float _skillMixSeconds = 0.08f;
    [SerializeField] private string[] _tierAttacks = { "attack_01", "attack_02", "attack_03", "attack_04" };
    private Drone_Betan _unit;
    private MeshRenderer _renderer;
    private int _tierIndex;
    private bool _skillSequence;
    private bool _inactivePose;
    private bool _paused;
    private bool _previousUnscaled;
    private float _authoredTimeScale;
    private Spine.Skeleton _cachedSkeleton;
    private ArmSlot[] _armSlots;
    private TrackEntry _activeSkillEntry;

    private sealed class ArmSlot
    {
        public Slot Slot;
        public int MinimumTier;
        public Attachment HiddenAttachment;
        public bool IsHidden;
    }

    /// <summary>외형 확인 및 미리보기에서 사용하는 Spine 인스턴스.</summary>
    public SkeletonAnimation Skeleton => _skeleton;
    /// <summary>현재 스킬 트랙에 저작된 발사 신호가 있어 유닛이 발사를 예약할 수 있는지.</summary>
    public bool HasSkillLaunchEvent => _activeSkillEntry != null && _skillLaunchEvent != null
        && _skillLaunchEvent.ContainsEvent(_activeSkillEntry.Animation);

    private void Awake()
    {
        _unit = GetComponent<Drone_Betan>();
        if (_skeleton == null) return;
        _renderer = _skeleton.GetComponent<MeshRenderer>();
        _authoredTimeScale = _skeleton.timeScale;
    }

    private void OnEnable()
    {
        ResetVisual();
        if (_skeleton != null)
        {
            _skeleton.BeforeApply += RestoreArmAttachments;
            _skeleton.UpdateComplete += FilterTierArms;
        }
        _unit?.onSkillFull?.AddListener(PlaySkill);
    }

    private void OnDisable()
    {
        _unit?.onSkillFull?.RemoveListener(PlaySkill);
        SetPauseIdle(false);
        if (_skeleton == null) return;
        _skeleton.BeforeApply -= RestoreArmAttachments;
        _skeleton.UpdateComplete -= FilterTierArms;
        RestoreArmAttachments();
        _skeleton.timeScale = _authoredTimeScale;
        _skeleton.AnimationState?.ClearTracks();
        _skillSequence = false;
    }

    private int CurrentTierIndex => _unit == null ? 0 : _unit.currentTier switch
    {
        Tier.Rare => 1,
        Tier.Epic => 2,
        Tier.Legend => 3,
        _ => 0
    };
    private string AttackName(int tier) => _tierAttacks != null && tier < _tierAttacks.Length ? _tierAttacks[tier] : string.Empty;
    private bool Has(string name) => !string.IsNullOrEmpty(name) && _skeleton != null && _skeleton.Skeleton?.Data.FindAnimation(name) != null;

    /// <summary>풀 재사용 시 이전 스킬 큐를 지우고 현재 등급의 팔을 전개한다.</summary>
    public void ResetVisual()
    {
        if (_skeleton == null) return;
        _skeleton.Initialize(false);
        if (_skeleton.AnimationState == null) return;
        RestoreArmAttachments();
        _skeleton.AnimationState.ClearTracks();
        _skeleton.Skeleton.SetupPose();
        CacheArmSlots();
        _tierIndex = CurrentTierIndex;
        _skillSequence = false;
        _inactivePose = false;
        StartWorking();
    }

    private void CacheArmSlots()
    {
        var skeleton = _skeleton?.Skeleton;
        if (skeleton == _cachedSkeleton) return;
        RestoreArmAttachments();
        _cachedSkeleton = skeleton;
        if (skeleton == null) { _armSlots = null; return; }
        var slots = new List<ArmSlot>();
        foreach (var slot in skeleton.Slots)
        {
            // Match the authored attack_01~04 arm order, including cables and lights.
            string name = slot.Data.Name;
            int minimumTier = BelongsToArm(name, "_RD") ? 0
                : BelongsToArm(name, "_LD") ? 1
                : BelongsToArm(name, "_RU") ? 2
                : BelongsToArm(name, "_LU") ? 3 : -1;
            if (minimumTier >= 0) slots.Add(new ArmSlot { Slot = slot, MinimumTier = minimumTier });
        }
        _armSlots = slots.ToArray();
    }

    private static bool BelongsToArm(string name, string group)
    {
        int index = name.LastIndexOf(group, StringComparison.Ordinal);
        int end = index + group.Length;
        return index >= 0 && (end == name.Length || name[end] == '_');
    }

    private void RestoreArmAttachments(ISkeletonAnimation animation) => RestoreArmAttachments();

    private void RestoreArmAttachments()
    {
        if (_armSlots == null) return;
        foreach (var arm in _armSlots)
        {
            if (!arm.IsHidden) continue;
            arm.Slot.Pose.Attachment = arm.HiddenAttachment;
            arm.HiddenAttachment = null;
            arm.IsHidden = false;
        }
    }

    private void FilterTierArms(ISkeletonRenderer renderer)
    {
        CacheArmSlots();
        if (_armSlots == null) return;
        foreach (var arm in _armSlots)
        {
            if (arm.MinimumTier <= _tierIndex) continue;
            arm.HiddenAttachment = arm.Slot.Pose.Attachment;
            arm.IsHidden = true;
            arm.Slot.Pose.Attachment = null;
        }
    }

    private void StartWorking()
    {
        _tierIndex = CurrentTierIndex;
        _skillSequence = false;
        string attack = AttackName(_tierIndex);
        var state = _skeleton.AnimationState;
        if (!Has(attack)) { PlayIdle(); return; }
        if (Has(attack + "_start"))
        {
            state.SetAnimation(0, attack + "_start", false);
            state.AddAnimation(0, attack, true, 0f);
        }
        else state.SetAnimation(0, attack, true);
    }

    private void PlayIdle()
    {
        _skillSequence = false;
        if (Has(_idle)) _skeleton.AnimationState.SetAnimation(0, _idle, true);
    }

    /// <summary>원본 스킬을 한 번 재생하고 등급별 공격으로 복귀한다. 저작된 버튼 이벤트가 유닛의 자폭 발사를 요청한다.</summary>
    public void PlaySkill()
    {
        if (_skeleton == null || _skeleton.AnimationState == null || !Has(_skill) || (_drag != null && _drag.IsDragging)) return;
        _tierIndex = CurrentTierIndex;
        string attack = AttackName(_tierIndex);
        var state = _skeleton.AnimationState;
        var skill = state.SetAnimation(0, _skill, false);
        _activeSkillEntry = skill;
        skill.Event += OnSkillEvent;
        skill.Interrupt += OnSkillInterrupted;
        skill.End += OnSkillInterrupted;
        skill.MixDuration = _skillMixSeconds;
        // skill already folds and deploys the arms. Repeating end/start reverses
        // that motion. Finish its full timeline, then blend directly to the loop.
        string next = Has(attack) ? attack : Has(_idle) ? _idle : null;
        if (next != null)
        {
            var loop = state.AddAnimation(0, next, true, skill.Animation.Duration);
            loop.MixDuration = _skillMixSeconds;
        }
        _skillSequence = true;
    }

    private void OnSkillEvent(TrackEntry entry, Spine.Event signal)
    {
        if (entry != _activeSkillEntry || _skillLaunchEvent == null || signal.Data.Name != _skillLaunchEvent.EventName) return;
        _unit?.LaunchPendingSkillBombs();
    }

    private void OnSkillInterrupted(TrackEntry entry)
    {
        entry.Event -= OnSkillEvent;
        entry.Interrupt -= OnSkillInterrupted;
        entry.End -= OnSkillInterrupted;
        if (entry != _activeSkillEntry) return;
        _activeSkillEntry = null;
        _unit?.CancelPendingSkillBombs();
    }

    /// <inheritdoc />
    public void SetPauseIdle(bool on)
    {
        if (_skeleton == null || on == _paused) return;
        _paused = on;
        if (on) { _previousUnscaled = _skeleton.UnscaledTime; _skeleton.UnscaledTime = true; }
        else
        {
            _skeleton.UnscaledTime = _previousUnscaled;
            if (!_skillSequence && !_inactivePose) StartWorking();
        }
    }

    private void LateUpdate()
    {
        if (_skeleton == null || _renderer == null || _source == null || _skeleton.AnimationState == null) return;
        _renderer.sortingLayerID = _source.sortingLayerID;
        _renderer.sortingOrder = _source.sortingOrder;
        var color = _source.color;
        _skeleton.Skeleton.SetColor(color.r, color.g, color.b, color.a);
        _skeleton.timeScale = _unit != null && _unit.IsStunned ? 0f : _authoredTimeScale;
        bool inactive = (_drag != null && _drag.IsDragging) || (_unit != null && _unit.IsCellSealed);
        if (inactive != _inactivePose)
        {
            _inactivePose = inactive;
            if (inactive) PlayIdle();
            else if (!_paused) StartWorking();
        }
        if (_inactivePose) return;
        var current = _skeleton.AnimationState.GetTrack(0);
        if (_skillSequence && (current == null || current.Loop)) _skillSequence = false;
        if (_paused)
        {
            if (current == null || (current.Loop && current.Animation.Name != _idle)) PlayIdle();
            return;
        }
        if (!_skillSequence && _tierIndex != CurrentTierIndex) StartWorking();
    }
}
