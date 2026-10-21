using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>동일 전투에서 일반 적중의 옅은 색 변화와 큰 스킬의 추가 Spine 피격 자세를 비교한다.</summary>
public sealed class BossHitReactionLab : MonoBehaviour
{
    [SerializeField] private CombatDistanceLab _combat;
    [SerializeField] private InGameLifetimeScope _scope;
    [SerializeField] private BossHitReactionSettings _settings;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private TMP_Text _pauseLabel;
    [SerializeField] private Image[] _buttons;
    [Inject] private DroneManager _drones;
    [Inject] private AlphanActiveSkill _chief;
    private SkeletonAnimation _skeleton;
    private Disigman _manualShooter;
    private readonly List<UnitData> _unitCopies = new();
    private readonly List<SkillData> _skillCopies = new();
    private readonly List<BoneState> _bones = new();
    private bool _applied;
    private float _elapsed = float.PositiveInfinity;
    private float _direction = 1f;
    private float _nextManualShot;
    private bool _paused;
    private int _mode = 1;
    private bool _chiefReaction;
    private ChiefConvergeBeamFx _chiefBeam;
    private float _chiefFallbackUntil;
    private float _chiefReleaseElapsed = -1f;
    private float _chiefReleaseFrom;
    private BossBase _tintBoss;
    private float _tintElapsed = float.PositiveInfinity;
    private Color _tintOriginal;
    private bool _tintApplied;

    private sealed class BoneState
    {
        public Bone Bone;
        public readonly BonePose Original = new();
    }

    /// <summary>실제 전투와 반응의 준비 완료 여부.</summary>
    public bool IsReady { get; private set; }
    /// <summary>현재 비교 모드. 0은 원본, 1은 추가 피격 자세.</summary>
    public int Mode => _mode;
    /// <summary>실제 큰 공격의 적중 횟수.</summary>
    public int ImpactCount { get; private set; }
    /// <summary>추가 반응을 시작한 횟수.</summary>
    public int ReactionCount { get; private set; }
    /// <summary>족장 집결 사격의 실제 적중 횟수. 연속 사격은 발마다 센다.</summary>
    public int ChiefImpactCount { get; private set; }
    /// <summary>현재 추가 자세의 강도.</summary>
    public float Intensity { get; private set; }
    /// <summary>실제 피해로 시작한 옅은 색 반응 횟수. 연타는 최소 간격으로 묶는다.</summary>
    public int TintCount { get; private set; }

    private void Start() => InitializeAsync().Forget();

    private async UniTaskVoid InitializeAsync()
    {
        var token=this.GetCancellationTokenOnDestroy();
        try
        {
            await UniTask.WaitUntil(()=>_combat != null && _combat.IsReady,cancellationToken:token);
            if(_scope == null)throw new InvalidOperationException("Reaction lab scope missing");
            _scope.Container.Inject(this);
            _combat.SelectPreset(0);
            _skeleton=_combat.Boss.GetComponentInChildren<SkeletonAnimation>();
            if(_skeleton == null || _settings == null)throw new InvalidOperationException("Reaction lab configuration missing");
            // This comparison owns the pose; the production prefab reaction must not apply it a second time.
            if(_combat.Boss.TryGetComponent<BossHitReaction>(out var productionReaction))productionReaction.enabled=false;
            foreach(string name in new[]{"body1","head","armL","armR"})
            {
                var bone=_skeleton.Skeleton.FindBone(name);
                if(bone == null)throw new InvalidOperationException("Missing reaction bone "+name);
                _bones.Add(new BoneState{Bone=bone});
            }
            _skeleton.BeforeApply+=BeforeApply;
            _skeleton.UpdateLocal+=ApplyPose;
            _skeleton.UpdateLocal+=ApplyTint;
            _tintBoss=_combat.Boss;
            _tintBoss.OnDamageDealt+=OnDamageDealt;
            foreach(var unit in FindObjectsByType<Disigman>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(unit.gameObject.scene != gameObject.scene)continue;
                var data=Instantiate(unit.unitData);
                var skill=Instantiate(data.skillData);
                skill.name=data.skillData.name+" (reaction lab runtime)";
                skill.hitEffects=new List<IEffect>(skill.hitEffects ?? new List<IEffect>());
                skill.hitEffects.Add(new BossHitReactionImpact(this));
                data.skillData=skill;
                unit.unitData=data;
                _unitCopies.Add(data);_skillCopies.Add(skill);
                _manualShooter=unit;
            }
            if(_manualShooter == null)throw new InvalidOperationException("No Disigman in lab");
            _drones.OnRallyImpact+=OnChiefImpact;
            IsReady=true;
            SelectMode(1);
        }
        catch(OperationCanceledException) { }
        catch(Exception error) { _status.text="비교를 준비하지 못했습니다";Debug.LogException(error,this); }
    }

    /// <summary>원본 반응과 추가 반응을 전환한다. 전투 상태나 진행 시간은 초기화하지 않는다.</summary>
    public void SelectMode(int mode)
    {
        _mode=Mathf.Clamp(mode,0,1);
        RestorePose();_elapsed=float.PositiveInfinity;Intensity=0f;
        ClearTint();
        _chiefReaction=false;_chiefBeam=null;_chiefReleaseElapsed=-1f;
        for(int i=0;i<_buttons.Length;i++)_buttons[i].color=i==_mode?new Color(.12f,.55f,.53f,.97f):new Color(.12f,.16f,.23f,.96f);
        if(_status != null)_status.text="피격 반응 비교 · "+(_mode==0?"현재":"충격을 받고 버티기");
    }

    private void OnDamageDealt(decimal damage, Vector3? position, BossDamageKind kind)
    {
        if (!IsReady || !isActiveAndEnabled || _mode == 0 || damage <= 0m || kind == BossDamageKind.Burn) return;
        float pulse = _settings.HitTintHoldSeconds + _settings.HitTintFadeSeconds;
        if (_tintElapsed < Mathf.Max(pulse, _settings.HitTintIntervalSeconds)) return;
        _tintElapsed = 0f;
        TintCount++;
    }

    private void ApplyTint(ISkeletonRenderer _)
    {
        RestoreTint();
        if (_mode == 0 || !isActiveAndEnabled || _tintBoss == null || _tintBoss.IsDead) return;
        float fade = Mathf.Max(.001f, _settings.HitTintFadeSeconds);
        float progress = Mathf.Clamp01((_tintElapsed - _settings.HitTintHoldSeconds) / fade);
        if (progress >= 1f) return;
        var skeleton = _skeleton.Skeleton;
        _tintOriginal = skeleton.GetColor();
        float strength = Mathf.Clamp01(_settings.HitTintStrength) * (1f - Mathf.SmoothStep(0f, 1f, progress));
        Color multiplier = Color.Lerp(Color.white, _settings.HitTint, strength);
        skeleton.SetColor(_tintOriginal.r * multiplier.r, _tintOriginal.g * multiplier.g,
            _tintOriginal.b * multiplier.b, _tintOriginal.a);
        _tintApplied = true;
    }

    private void RestoreTint()
    {
        if (!_tintApplied || _skeleton == null) return;
        _skeleton.Skeleton.SetColor(_tintOriginal.r, _tintOriginal.g, _tintOriginal.b, _tintOriginal.a);
        _tintApplied = false;
    }

    private void ClearTint()
    {
        RestoreTint();
        _tintElapsed = float.PositiveInfinity;
    }

    private void OnDisable() => ClearTint();

    /// <summary>실제 스킬 투사체의 적중 효과에서만 받는 추가 자세 시작 요청.</summary>
    public void OnHeavyImpact(BossBase target,Vector3 hitPosition)
    {
        if(!IsReady || target != _combat.Boss || target.IsDead)return;
        ImpactCount++;
        if(_mode==0 || IsPatternPlaying() || _chiefReaction)return;
        _direction=hitPosition.x>=target.transform.position.x?1f:-1f;
        _elapsed=0f;ReactionCount++;
    }

    private void OnChiefImpact(BossBase target)
    {
        if(!IsReady || target != _combat.Boss || target.IsDead)return;
        ChiefImpactCount++;
        ImpactCount++;
        if(_mode==0 || IsPatternPlaying())return;
        // Resolve at actual damage only; gathering and beam charge never start the pose.
        if(_chiefBeam == null || !_chiefBeam.IsPlaying)
            foreach(var beam in FindObjectsByType<ChiefConvergeBeamFx>(FindObjectsSortMode.None))
                if(beam.gameObject.scene==gameObject.scene && beam.FiringRemainingSeconds>0f){_chiefBeam=beam;break;}
        _chiefFallbackUntil=Time.time+_settings.ChiefFallbackHoldSeconds;
        if(!_chiefReaction){_elapsed=0f;_direction=1f;}
        _chiefReaction=true;_chiefReleaseElapsed=-1f;
        ReactionCount++;
    }

    /// <summary>실제 족장 액티브를 충전 대기 없이 발동한다. 집결·빔·피해·쿨타임은 기존 경로를 따른다.</summary>
    public void ShowChiefAttack()
    {
        if(!IsReady || Time.timeScale<=0f || !_chief.IsAvailable || _drones.IsRallying || _drones.RallyAvailableDroneCount==0)return;
        _chief.Advance(_chief.CooldownSeconds);
        _chief.TryActivate();
    }

    /// <summary>충전 대기 없이 기존 스킬 투사체·효과를 발사해 동일한 적중을 바로 비교한다.</summary>
    public void ShowHeavyAttack()
    {
        if(!IsReady || _manualShooter == null || Time.timeScale<=0f || Time.time<_nextManualShot)return;
        if(_manualShooter.IsStunned || !_manualShooter.isActiveAndEnabled)return;
        var skill=_manualShooter.unitData.skillData;
        if(skill.action is not DisigmanSkillAction action)return;
        var projectile=action.projectileDataId==0?null:Table.Projectile.Get(action.projectileDataId);
        _manualShooter.Combat.LaunchProjectile(skill.hitEffects,skill.additionalEffects,action.sizeMultiplier,projectile);
        _nextManualShot=Time.time+_settings.ManualShotInterval;
    }

    /// <summary>실제 전투와 피격 자세를 함께 정지하거나 재생한다.</summary>
    public void TogglePause()
    {
        if(!IsReady)return;
        _combat.TogglePause();_paused=!_paused;
        _pauseLabel.text=_paused?"전투 재생":"일시정지";
    }

    private void Update()
    {
        if(!IsReady)return;
        _tintElapsed+=Time.deltaTime;
        _elapsed+=Time.deltaTime;
        if(_chiefReaction){UpdateChiefReaction();return;}
        float attack=Mathf.Max(.001f,_settings.AttackSeconds);
        float hold=attack+_settings.HoldSeconds;
        float end=hold+Mathf.Max(.001f,_settings.RecoverSeconds);
        Intensity=_elapsed<attack?Mathf.Sin(_elapsed/attack*Mathf.PI*.5f)
            :_elapsed<hold?1f:_elapsed<end?1f-Mathf.SmoothStep(0f,1f,(_elapsed-hold)/(end-hold)):0f;
    }

    private void UpdateChiefReaction()
    {
        float attack=Mathf.Max(.001f,_settings.ChiefAttackSeconds);
        bool holding=_chiefBeam != null ? _chiefBeam.FiringRemainingSeconds>0f : Time.time<_chiefFallbackUntil;
        if(_elapsed<attack)
            Intensity=Mathf.Sin(_elapsed/attack*Mathf.PI*.5f);
        else if(holding)
        {
            float settle=(_elapsed-attack)/Mathf.Max(.001f,_settings.ChiefSettleSeconds);
            Intensity=Mathf.Lerp(1f,_settings.ChiefSustainIntensity,Mathf.SmoothStep(0f,1f,settle));
            _chiefReleaseElapsed=-1f;
        }
        else
        {
            if(_chiefReleaseElapsed<0f){_chiefReleaseElapsed=0f;_chiefReleaseFrom=Intensity;}
            _chiefReleaseElapsed+=Time.deltaTime;
            float recovery=_chiefReleaseElapsed/Mathf.Max(.001f,_settings.ChiefRecoverSeconds);
            Intensity=_chiefReleaseFrom*(1f-Mathf.SmoothStep(0f,1f,recovery));
            if(recovery>=1f){_chiefReaction=false;_chiefBeam=null;_elapsed=float.PositiveInfinity;}
        }
    }

    private bool IsPatternPlaying()
    {
        string name=_skeleton?.AnimationState?.GetTrack(0)?.Animation?.Name;
        return name != null && name.StartsWith("skill_",StringComparison.Ordinal);
    }

    private void BeforeApply(ISkeletonAnimation _)
    {
        RestorePose();
        RestoreTint();
    }

    private void ApplyPose(ISkeletonRenderer _)
    {
        RestorePose();
        if(_mode==0 || Intensity<=0f || IsPatternPlaying())return;
        foreach(var state in _bones)state.Original.Set(state.Bone.Pose);
        _applied=true;
        var body=_bones[0].Bone.Pose;
        float compression=_chiefReaction?_settings.ChiefBodyCompression:_settings.BodyCompression;
        float expansion=_chiefReaction?_settings.ChiefBodyExpansion:_settings.BodyExpansion;
        float tilt=_chiefReaction?_settings.ChiefBodyTilt:_settings.BodyTilt;
        float head=_chiefReaction?_settings.ChiefHeadTilt:_settings.HeadTilt;
        float arms=_chiefReaction?_settings.ChiefArmBrace:_settings.ArmBrace;
        float strain=_chiefReaction ? Mathf.Sin(_elapsed*_settings.ChiefStrainFrequency*Mathf.PI*2f)
            *_settings.ChiefStrainDegrees*Intensity*Intensity : 0f;
        // Crocus body1 is authored at 90 degrees: local X is the vertical body axis.
        body.ScaleX*=1f-compression*Intensity;
        body.ScaleY*=1f+expansion*Intensity;
        body.Rotation+=tilt*Intensity*_direction+strain;
        _bones[1].Bone.Pose.Rotation-=head*Intensity*_direction+strain;
        _bones[2].Bone.Pose.Rotation+=arms*Intensity;
        _bones[3].Bone.Pose.Rotation-=arms*Intensity;
    }

    private void RestorePose()
    {
        if(!_applied)return;
        foreach(var state in _bones)state.Bone.Pose.Set(state.Original);
        _applied=false;
    }

    private void OnDestroy()
    {
        if(_drones != null)_drones.OnRallyImpact-=OnChiefImpact;
        if(_tintBoss != null)_tintBoss.OnDamageDealt-=OnDamageDealt;
        ClearTint();
        RestorePose();
        if(_skeleton != null){_skeleton.BeforeApply-=BeforeApply;_skeleton.UpdateLocal-=ApplyPose;_skeleton.UpdateLocal-=ApplyTint;}
        foreach(var data in _unitCopies)if(data != null)Destroy(data);
        foreach(var skill in _skillCopies)if(skill != null)Destroy(skill);
    }
}
