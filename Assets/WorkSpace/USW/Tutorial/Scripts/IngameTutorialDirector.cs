using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using HSD.UI.Upgrade;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GaeGGUL.Tutorial
{
    /// <summary>Scene-owned orchestration of the first battle using actual gameplay and UI systems.</summary>
    public sealed class IngameTutorialDirector : MonoBehaviour
    {
        [SerializeField] private IngameTutorialPlan _plan;
        [SerializeField] private TutorialTargetBinding[] _targets;
        private readonly System.Collections.Generic.Dictionary<string, int> _signals = new System.Collections.Generic.Dictionary<string, int>();
        private int _lessonIndex;
        private int _lessonCount;
        private IngameTutorialStep _currentRecipe;
        private readonly object _customPauseOwner = new object();
        [SerializeField] private IngameTutorialSettings _settings;
        [SerializeField] private IngameTutorialOverlay _overlay;
        [SerializeField] private IngameTutorialDialogue _dialogue;
        [SerializeField] private Button _summonButton;
        [SerializeField] private CanvasGroup _summonGroup;
        [SerializeField] private CanvasGroup _currencyGroup;
        [SerializeField] private RectTransform _timerTarget;
        [SerializeField] private RectTransform _experienceTarget;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _chiefButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private CanvasGroup[] _bossHud;
        [SerializeField] private CanvasGroup _upgradeGroup;
        [SerializeField] private CanvasGroup _chiefGroup;
        [SerializeField] private CanvasGroup _inventoryGroup;
        [SerializeField] private UI_UpgradePanel _upgradePanel;
        [SerializeField] private RectTransform _upgradeSlots;
        [SerializeField] private LevelUpUI _levelUpUI;
        [SerializeField] private TotemRewardUI _rewardUI;
        [SerializeField] private TotemInventoryUI _inventoryUI;
        [Inject] private GameManager _game;
        [Inject] private GameInitializer _initializer;
        [Inject] private WaveManager _wave;
        [Inject] private UnitSpawner _spawner;
        [Inject] private GridManager _grid;
        [Inject] private CurrencyManager _currency;
        [Inject] private ExpManager _exp;
        [Inject] private BossManager _boss;
        [Inject] private TimeScaleService _time;
        [Inject] private InputManager _input;
        [Inject] private TotemInventory _inventory;
        [Inject] private BossPatternController _patterns;
        [Inject] private AlphanActiveSkill _chiefSkill;
        [Inject] private FieldPauseVisuals _fieldPause;
        [Inject] private IngameTutorialProgress _progress;
        [Inject] private SceneChangeManager _scenes;
        [SerializeField] private Button _completeButton;
        private bool _returning;
        private BossBase _guidedBoss;
        private CancellationTokenSource _cts;
        private Camera _camera;
        private bool _income;
        private bool _running;
        private bool _hasCameraPose;
        private Vector3 _cameraPosition;
        private float _cameraSize;
        private float _cameraFov;
        private UnitBase _firstUnit;
        private UnitBase _mergedUnit;
        private TotemBase _placedTotem;
        private GridCell _dropCell;

        /// <summary>Current authored lesson, visible to diagnostics and playtest tools.</summary>
        public IngameTutorialStage CurrentStage { get; private set; }
        /// <summary>True only after all authored interactions have succeeded.</summary>
        public bool IsComplete { get; private set; }

        private void Start()
        {
            if (_completeButton != null)
            {
                _completeButton.gameObject.SetActive(false);
                _completeButton.onClick.AddListener(ReturnToLobby);
            }
            RunAsync().Forget();
        }

        private async UniTaskVoid RunAsync()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            var token = _cts.Token;
            try
            {
                ValidateConfiguration();
                _camera = Camera.main;
                _running = true;
                _exp.DeferLevelUps = true;
                _overlay.Show(_settings, false, false, false, IngameTutorialOverlay.Gesture.Tap);
                foreach (var group in _bossHud) SetVisible(group, false);
                SetVisible(_upgradeGroup, false); SetVisible(_chiefGroup, false); SetVisible(_inventoryGroup, false);
                SetVisible(_summonGroup, false); SetVisible(_currencyGroup, false);
                _input.AllowPointerClicks = false;
                _input.CanBeginInteraction = _ => false;
                // Let gameplay Start methods wire their injected services before starting the battle.
                await Until(() => _initializer.IsReady, token);
                foreach (var data in _settings.SpawnUnits.Append(_settings.MergeUnit).Distinct())
                    await data.LoadAssetsAsync().AttachExternalCancellation(token);
                _game.StartGame(false);
                var lessons = _plan.Lessons;
                _lessonCount = lessons.Count;
                foreach (var asset in lessons)
                {
                    _lessonIndex++;
                    if (asset.Kind != IngameTutorialLesson.LessonKind.GameplayRecipe)
                    {
                        await ExecuteCustomAsync(asset, token);
                        continue;
                    }
                    var lesson = asset.Gameplay;
                    _currentRecipe = lesson;
                    _dialogue.Hide();
                    if (lesson.delayBeforeExecute > 0) await Delay(lesson.delayBeforeExecute, token);
                    CurrentStage = lesson.Stage;
                    Debug.Log($"[IngameTutorial] {(int)CurrentStage}: {CurrentStage}", this);
                    try { await lesson.ExecuteAsync(this, token); }
                    finally
                    {
                        if (_dialogue != null) _dialogue.Hide();
                        if (_overlay != null) _overlay.Hide();
                        _time?.Release(this);
                        if (_running && _input != null)
                        {
                            _input.CanBeginInteraction = _ => false;
                            _input.CanEndInteraction = null;
                        }
                    }
                }
                IsComplete = true;
                Debug.Log("[IngameTutorial] Complete", this);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e, this); }
            finally
            {
                Restore();
                _cts?.Dispose(); _cts = null;
            }
            if (IsComplete)
            {
                _progress.Complete();
                if (_completeButton != null) _completeButton.gameObject.SetActive(true);
            }
        }

        /// <summary>완료 안내 버튼에서 로비로 복귀한다. 다시 배우기는 로비에서 선택한다.</summary>
        public void ReturnToLobby() => ReturnToLobbyAsync().Forget();

        private async UniTask ReturnToLobbyAsync()
        {
            if (!IsComplete || _returning) return;
            _returning = true;
            try { await _scenes.TransitionToSceneAsync("LobbyScene"); }
            catch (Exception error) { Debug.LogWarning("튜토리얼 로비 복귀 실패: " + error.Message); }
            finally { _returning = false; }
        }

        private void ValidateConfiguration()
        {
            if (_plan == null || _settings == null || _overlay == null || _dialogue == null || _game == null)
                throw new InvalidOperationException("Tutorial scene configuration or DI is missing.");
            if (_settings.SpawnUnits == null || _settings.SpawnUnits.Length != 4 ||
                _settings.SpawnCells == null || _settings.SpawnCells.Length != 4 ||
                _settings.SpawnUnits.Any(d => d == null) || _settings.SpawnUnits[0] != _settings.SpawnUnits[1])
                throw new InvalidOperationException("Tutorial requires four authored summons and a matching first pair.");
            if (_settings.MergeUnit == null || _settings.LevelUpChoices == null || _settings.LevelUpChoices.Length != 3 ||
                _settings.LevelUpChoices.Any(c => c == null) || _settings.TotemChoices == null || _settings.TotemChoices.Length != 3 ||
                _settings.TotemChoices.Any(t => t == null) || _settings.RequiredTotem == null || !_settings.RequiredTotem.isRotatable ||
                !_settings.TotemChoices.Contains(_settings.RequiredTotem) || _settings.CounterPattern == null ||
                _settings.CounterPattern.patternType != BossPatternType.Earthquake || _settings.CounterPattern.ImpactTimeSec <= 0)
                throw new InvalidOperationException("Tutorial requires a fixed merge unit, three choices and three totems including a required rotatable reward.");
            if (_plan != null)
            {
                if (_targets != null && _targets.Where(t => t != null).GroupBy(t => t.Key).Any(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1))
                    throw new InvalidOperationException("Tutorial target keys must be non-empty and unique.");
                var planError = _plan.Validate();
                if (planError != null) throw new InvalidOperationException(planError);
                if (_plan.Lessons.Count == 0) throw new InvalidOperationException("Tutorial sequence is empty.");
                foreach (var lesson in _plan.Lessons)
                {
                    if (lesson == null) throw new InvalidOperationException("Tutorial sequence contains an empty lesson slot.");
                    var error = lesson.Validate();
                    if (error != null) throw new InvalidOperationException(lesson.name + ": " + error);
                    if (lesson.Kind == IngameTutorialLesson.LessonKind.GameplayRecipe) continue;
                    var target = ResolveTarget(lesson.TargetKey);
                    if (!string.IsNullOrEmpty(lesson.TargetKey) && (target == null || target.Highlight == null))
                        throw new InvalidOperationException(lesson.name + ": target binding is missing: " + lesson.TargetKey);
                    if (lesson.Kind == IngameTutorialLesson.LessonKind.ForceButton && target.Button == null)
                        throw new InvalidOperationException(lesson.name + ": target Button is missing.");
                }
            }
        }

        /// <summary>UnityEvent-compatible completion/start signal; signals are observed only after the relevant wait begins.</summary>
        public void Signal(string key)
        {
            if (!string.IsNullOrWhiteSpace(key)) _signals[key] = SignalCount(key) + 1;
        }

        private int SignalCount(string key) => !string.IsNullOrEmpty(key) && _signals.TryGetValue(key, out var count) ? count : 0;
        private TutorialTargetBinding ResolveTarget(string key) => _targets?.SingleOrDefault(t => t != null && t.Key == key);
        private void ShowRecipeDialogue() => _dialogue.Show(_currentRecipe, _lessonCount, _lessonIndex);

        private async UniTask ExecuteCustomAsync(IngameTutorialLesson lesson, CancellationToken token)
        {
            _dialogue.Hide(); _overlay.Hide();
            var previousBegin = _input.CanBeginInteraction;
            var previousEnd = _input.CanEndInteraction;
            bool previousClicks = _input.AllowPointerClicks;
            // A signal can come from actual world input while this lesson waits for its trigger/result.
            if (lesson.AllowWorldInput)
            {
                _input.CanBeginInteraction = null; _input.CanEndInteraction = null; _input.AllowPointerClicks = true;
            }
            try { await ExecuteCustomBodyAsync(lesson, token); }
            finally
            {
                if (_running && _input != null)
                {
                    _input.CanBeginInteraction = previousBegin;
                    _input.CanEndInteraction = previousEnd;
                    _input.AllowPointerClicks = previousClicks;
                }
            }
        }

        private async UniTask ExecuteCustomBodyAsync(IngameTutorialLesson lesson, CancellationToken token)
        {
            var target = ResolveTarget(lesson.TargetKey);
            if (lesson.StartWhen == IngameTutorialLesson.StartCondition.TargetVisible)
                await Until(() => target.IsVisible, token);
            if (lesson.StartWhen == IngameTutorialLesson.StartCondition.Signal)
            {
                int before = SignalCount(lesson.StartSignal);
                await Until(() => SignalCount(lesson.StartSignal) > before, token);
            }
            await Delay(lesson.DelaySeconds, token);
            bool awareness = lesson.Kind == IngameTutorialLesson.LessonKind.Awareness;
            bool button = lesson.Kind == IngameTutorialLesson.LessonKind.ForceButton;
            if (lesson.PauseGameplay) _time.Pause(_customPauseOwner);
            _dialogue.ShowText(lesson.Instruction, _lessonIndex, _lessonCount, awareness);
            var areas = target == null ? Array.Empty<Func<Rect>>() : new[] { Ui(target.Highlight) };
            _overlay.Show(_settings, button, lesson.Dim, lesson.ShowHand && button, IngameTutorialOverlay.Gesture.Tap, areas);
            try
            {
                if (awareness)
                {
                    await AcknowledgeAsync(token);
                }
                else if (button)
                {
                    bool clicked = false;
                    void Click() => clicked = true;
                    target.Button.onClick.AddListener(Click);
                    try { await Until(() => clicked, token); }
                    finally { if (target.Button != null) target.Button.onClick.RemoveListener(Click); }
                }
                else
                {
                    int before = SignalCount(lesson.CompletionSignal);
                    _overlay.Hide();
                    await Until(() => SignalCount(lesson.CompletionSignal) > before, token);
                }
            }
            finally
            {
                if (_dialogue != null) _dialogue.Hide();
                if (_overlay != null) _overlay.Hide();
                if (lesson.PauseGameplay) _time?.Release(_customPauseOwner);
            }
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        }

        private void Update()
        {
            if (_income && _running && _game.CurrentState == GameManager.GameState.Playing)
                _currency.AddCurrency(_settings.TrainingFoodPerSecond * Time.deltaTime);
        }

        /// <summary>Executes a lesson against actual scene state, with cancellation owned by this scene.</summary>
        public async UniTask ExecuteStageAsync(IngameTutorialStage stage, CancellationToken token)
        {
            switch (stage)
            {
                case IngameTutorialStage.FirstSummon:
                    Block();
                    _income = false;
                    await UniTask.WhenAll(RevealAsync(_summonGroup, token), RevealAsync(_currencyGroup, token));
                    int summons = _settings.SpawnUnits.Length;
                    float budget = summons * _spawner.CurrentCost + _settings.CostIncrease * summons * (summons - 1) / 2f;
                    _currency.AddCurrency(Mathf.Max(0, budget - _currency.Currency));
                    ShowRecipeDialogue();
                    _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, ButtonArea(_summonButton));
                    await Until(() => _spawner.SuccessfulSpawnCount >= summons, token);
                    Block();
                    await Until(() => _grid.GetOccupiedCells().All(c => c.OccupyingUnit == null || c.OccupyingUnit.gameObject.activeInHierarchy), token);
                    _firstUnit = _grid.GetCell(_settings.SpawnCells[0]).OccupyingUnit;
                    break;
                case IngameTutorialStage.BossEntrance:
                    await BossEntranceAsync(token);
                    break;
                case IngameTutorialStage.TimeLimit:
                    ShowRecipeDialogue();
                    await AwarenessAsync(Ui(_timerTarget), token);
                    break;
                case IngameTutorialStage.ObserveCombat:
                    Block();
                    await Delay(_settings.ObserveCombatSeconds, token);
                    break;
                case IngameTutorialStage.ExperienceGauge:
                    ShowRecipeDialogue();
                    await AwarenessAsync(Ui(_experienceTarget), token);
                    _time.Release(this);
                    _overlay.Show(_settings, false, true, false, IngameTutorialOverlay.Gesture.Tap, Ui(_experienceTarget));
                    float startExp = _exp.CurrentExp;
                    float requiredExp = _exp.ExpToLevelUp;
                    const int fillSteps = 20;
                    for (int i = 1; i <= fillSteps; i++)
                    {
                        await Delay(_settings.ExperienceFillSeconds / fillSteps, token);
                        _exp.AddExp(Mathf.Max(0, Mathf.Lerp(startExp, requiredExp, i / (float)fillSteps) - _exp.CurrentExp));
                    }
                    await Delay(0.4f, token);
                    break;
                case IngameTutorialStage.ThreeSummons:
                    Block();
                    float total = _spawner.CurrentCost * 3 + _settings.CostIncrease * 3;
                    await Until(() => _currency.Currency >= total, token);
                    ShowRecipeDialogue();
                    _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, ButtonArea(_summonButton));
                    for (int i = 0; i < 3; i++) await SummonAsync(token);
                    break;
                case IngameTutorialStage.Merge:
                    ShowRecipeDialogue();
                    await MergeAsync(token);
                    break;
                case IngameTutorialStage.LevelUp:
                    Block();
                    _levelUpUI.RequireUnitPreview = true;
                    _exp.DeferLevelUps = false;
                    _exp.AddExp(0);
                    await Until(() => _levelUpUI.IsReadyForSelection, token);
                    ShowRecipeDialogue();
                    if (_boss.CurrentBoss != null) _boss.CurrentBoss.Invincible = true;
                    await AwarenessAsync(Ui(_levelUpUI.ChoiceArea), token);
                    await PreviewChoiceAsync(token);
                    _overlay.Hide(); _time.Release(this);
                    await Until(() => _game.CurrentState == GameManager.GameState.Playing && !_levelUpUI.IsReadyForSelection, token);
                    _exp.DeferLevelUps = true;
                    _levelUpUI.RequireUnitPreview = false;
                    break;
                case IngameTutorialStage.OpenUpgrade:
                    Block();
                    _time.Pause(this);
                    _currency.AddCurrency(Mathf.Max(0, _settings.UpgradeFoodThreshold - _currency.Currency));
                    await RevealAsync(_upgradeGroup, token);
                    ShowRecipeDialogue();
                    _time.Pause(this);
                    _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, ButtonArea(_upgradeButton));
                    await Until(() => _upgradePanel.IsOpen, token);
                    break;
                case IngameTutorialStage.UpgradeSlots:
                    Block();
                    _time.Pause(this);
                    await Delay(_settings.RevealSeconds, token);
                    ShowRecipeDialogue();
                    var item = _upgradeSlots.GetComponentsInChildren<UI_UpgradeItem>().First(i => i.UpgradeTarget == _settings.UpgradeTarget && i.CanUpgrade);
                    _currency.AddCurrency(Mathf.Max(0, item.CurrentCost - _currency.Currency));
                    int level = item.CurrentLevel;
                    _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, Ui((RectTransform)item.transform));
                    await Until(() => item.CurrentLevel > level, token);
                    Block();
                    _dialogue.ShowText("좋아요! 유닛이 더 강해졌어요!", _lessonIndex, _lessonCount, false);
                    await Delay(1.2f, token);
                    await _upgradePanel.CloseAsync().AttachExternalCancellation(token);
                    _income = false;
                    break;
                case IngameTutorialStage.ChiefSkill:
                    Block();
                    await Until(() => Time.timeScale > 0, token);
                    bool countered = false;
                    var chiefScale = _chiefGroup.transform.localScale;
                    void Counter(BossBase boss, BossPatternData pattern)
                    {
                        if (boss == _guidedBoss && pattern == _settings.CounterPattern)
                        {
                            countered = true;
                            _patterns.UnregisterBoss(boss);
                            _fieldPause.Exit(this);
                        }
                    }
                    void Warn(BossPatternData pattern)
                    {
                        if (pattern == _settings.CounterPattern) _fieldPause.HoldAttacks(this);
                    }
                    _patterns.OnPatternCountered += Counter;
                    _guidedBoss.OnPatternStarted += Warn;
                    try
                    {
                        _patterns.RegisterBoss(_guidedBoss, new[] { _settings.CounterPattern });
                        await Until(() => _patterns.IsCounterablePattern(_guidedBoss), token);
                        _chiefSkill.Advance(_chiefSkill.CooldownSeconds);
                        await Until(() => _chiefSkill.CanActivate, token);
                        _chiefGroup.transform.localScale = chiefScale * 0.85f;
                        await UniTask.WhenAll(RevealAsync(_chiefGroup, token),
                            _chiefGroup.transform.DOScale(chiefScale, _settings.RevealSeconds).SetEase(Ease.OutBack)
                                .SetUpdate(true).ToUniTask(cancellationToken: token));
                        ShowRecipeDialogue();
                        _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, Ui((RectTransform)_chiefButton.transform));
                        await Until(() => countered, token);
                        Block();
                        _dialogue.ShowText("좋아요! 족장 스킬로 보스의 지진을 막았어요!", _lessonIndex, _lessonCount, false);
                        await Delay(1.5f, token);
                    }
                    finally
                    {
                        _chiefGroup.transform.DOKill();
                        _chiefGroup.transform.localScale = chiefScale;
                        _patterns.OnPatternCountered -= Counter;
                        if (_guidedBoss != null) _guidedBoss.OnPatternStarted -= Warn;
                        _patterns.UnregisterBoss(_guidedBoss);
                        _fieldPause.Exit(this);
                    }
                    if (_guidedBoss != null) _guidedBoss.Invincible = false;
                    break;
                case IngameTutorialStage.TotemChoice:
                    await ChooseTotemAsync(token);
                    break;
                case IngameTutorialStage.OpenInventory:
                    _input.CanBeginInteraction = _ => false; _input.AllowPointerClicks = false;
                    _time.Pause(this);
                    Block();
                    await RevealAsync(_inventoryGroup, token);
                    ShowRecipeDialogue();
                    _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, Ui((RectTransform)_inventoryButton.transform));
                    await Until(() => _inventoryUI.IsOpen, token);
                    break;
                case IngameTutorialStage.PlaceTotem:
                    _time.Pause(this); ShowRecipeDialogue();
                    await PlaceTotemAsync(token);
                    break;
                case IngameTutorialStage.MoveTotem:
                    _time.Pause(this); ShowRecipeDialogue();
                    await MoveTotemAsync(token);
                    break;
                case IngameTutorialStage.RotateTotem:
                    _time.Pause(this); ShowRecipeDialogue();
                    await RotateTotemAsync(token);
                    break;
            }
        }

        private async UniTask SummonAsync(CancellationToken token)
        {
            int count = _spawner.SuccessfulSpawnCount;
            _time.Pause(this);
            _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, ButtonArea(_summonButton));
            await Until(() => _spawner.SuccessfulSpawnCount > count, token);
            Block();
            _time.Release(this);
            await Until(() => _spawner.LastSpawnedUnit != null && _spawner.LastSpawnedUnit.gameObject.activeInHierarchy, token);
        }

        private async UniTask BossEntranceAsync(CancellationToken token)
        {
            Block();
            _time.Pause(this);
            if (_camera != null)
            {
                _cameraPosition = _camera.transform.position; _cameraSize = _camera.orthographicSize;
                _cameraFov = _camera.fieldOfView; _hasCameraPose = true;
                var target = _boss.SpawnPosition; target.z = _cameraPosition.z;
                await DOTween.Sequence().SetUpdate(true)
                    .Append(_camera.transform.DOShakePosition(0.2f, new Vector3(0.08f, 0.08f, 0), 12, 90, false, true))
                    .AppendInterval(0.12f)
                    .Append(_camera.transform.DOShakePosition(0.26f, new Vector3(0.14f, 0.14f, 0), 14, 90, false, true))
                    .AppendInterval(0.12f)
                    .Append(_camera.transform.DOMove(target, _settings.CameraZoomSeconds))
                    .Join(_camera.orthographic
                        ? _camera.DOOrthoSize(_cameraSize * _settings.CameraZoomRatio, _settings.CameraZoomSeconds)
                        : _camera.DOFieldOfView(_cameraFov * _settings.CameraZoomRatio, _settings.CameraZoomSeconds))
                    .ToUniTask(cancellationToken: token);
            }
            _time.Release(this);
            _wave.StartWave();
            await Until(() => _boss.CurrentBoss != null, token);
            _boss.CurrentBoss.Invincible = true;
            _guidedBoss = _boss.CurrentBoss;
            _patterns.UnregisterBoss(_guidedBoss);
            _time.Pause(this);
            if (_camera != null)
            {
                var zoom = DOTween.Sequence().SetUpdate(true)
                    .Append(_camera.transform.DOShakePosition(_settings.CameraHoldSeconds, new Vector3(0.12f, 0.12f, 0), 18, 90, false, true))
                    .Append(_camera.transform.DOMove(_cameraPosition, _settings.CameraZoomSeconds))
                    .Join(_camera.orthographic
                        ? _camera.DOOrthoSize(_cameraSize, _settings.CameraZoomSeconds)
                        : _camera.DOFieldOfView(_cameraFov, _settings.CameraZoomSeconds));
                await zoom.ToUniTask(cancellationToken: token);
                _hasCameraPose = false;
            }
            foreach (var group in _bossHud) await RevealAsync(group, token);
            _time.Release(this);
        }

        private async UniTask MergeAsync(CancellationToken token)
        {
            var other = _grid.GetCell(_settings.SpawnCells[1]).OccupyingUnit;
            if (_firstUnit == null || other == null || !UnitMergeRules.CanPair(_firstUnit, other))
                throw new InvalidOperationException("Authored tutorial merge pair is unavailable.");
            var source = _firstUnit.currentCell; var target = other.currentCell;
            var a = _firstUnit.GetComponent<DragHandler>(); var b = other.GetComponent<DragHandler>();
            _time.Pause(this);
            _input.CanBeginInteraction = d => ReferenceEquals(d, a) || ReferenceEquals(d, b);
            _input.CanEndInteraction = (d, p) => IsCellAt(p, ReferenceEquals(d, a) ? target : source);
            _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Drag, World(_firstUnit.currentCell), World(other.currentCell));
            _input.AllowPointerClicks = false;
            // Only the tutorial overlay should receive UI clicks during the forced merge.
            var uiRaycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None)
                .Where(r => r.isActiveAndEnabled && !_overlay.transform.IsChildOf(r.transform)).ToArray();
            foreach (var raycaster in uiRaycasters) raycaster.enabled = false;
            try
            {
                await Until(() => _firstUnit == null && other == null &&
                    (source.OccupyingUnit != null || target.OccupyingUnit != null), token);
            }
            finally
            {
                foreach (var raycaster in uiRaycasters)
                    if (raycaster != null) raycaster.enabled = true;
            }
            _input.CanBeginInteraction = _ => false; _input.CanEndInteraction = null;
            Block(); _time.Release(this);
            await Until(() => _grid.GetOccupiedCells().All(c => c.OccupyingUnit == null || c.OccupyingUnit.gameObject.activeInHierarchy), token);
            _mergedUnit = source.OccupyingUnit != null ? source.OccupyingUnit : target.OccupyingUnit;
        }

        private async UniTask PlaceTotemAsync(CancellationToken token)
        {
            if (!_inventory.Items[0].isRotatable) throw new InvalidOperationException("Tutorial reward must be rotatable.");
            _dropCell = _grid.GetEmptyCells().OrderBy(c => c.GridPosition.y).ThenBy(c => c.GridPosition.x).First();
            await Delay(_settings.RevealSeconds, token);
            _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Drag, Ui(_inventoryUI.GetSlotRect(0)), World(_dropCell));
            var previousPlacement = _inventory.CanPlace;
            bool previousClose = _inventoryUI.AllowClose;
            _inventoryUI.AllowClose = false;
            _inventory.CanPlace = (index, cell) => index == 0 && cell == _dropCell;
            try
            {
                while (_dropCell.OccupyingTotem == null)
                {
                    // An invalid/cancelled drag closes the normal drawer. Reopen for another attempt.
                    if (!_inventoryUI.IsOpen && !_inventoryUI.IsDraggingOrPlacing && _inventory.Items.Count > 0)
                        _inventoryUI.OpenForTutorialRetry();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally { _inventory.CanPlace = previousPlacement; _inventoryUI.AllowClose = previousClose; }
            _placedTotem = _dropCell.OccupyingTotem;
            await Until(() => !_inventoryUI.IsDraggingOrPlacing, token);
        }

        private async UniTask MoveTotemAsync(CancellationToken token)
        {
            var source = _placedTotem.CurrentCell;
            // Leave a tile between the source and destination so the two spotlights stay distinct.
            var destination = _grid.GetEmptyCells()
                .Where(c => c != source && (c.GridPosition - source.GridPosition).sqrMagnitude >= 4)
                .OrderBy(c => Mathf.Abs(c.GridPosition.y - source.GridPosition.y))
                .ThenBy(c => (c.GridPosition - source.GridPosition).sqrMagnitude)
                .ThenBy(c => c.GridPosition.x)
                .First();
            _dropCell = destination;
            var drag = _placedTotem.GetComponent<DragHandler>();
            _input.CanBeginInteraction = d => ReferenceEquals(d, drag);
            _input.CanEndInteraction = (d, p) => !drag.IsRotating && IsCellAt(p, destination);
            _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Drag, World(source), World(destination));
            await Until(() => _placedTotem.CurrentCell == destination && source.OccupyingTotem != _placedTotem, token);
        }

        private async UniTask RotateTotemAsync(CancellationToken token)
        {
            int rotation = _placedTotem.RotationStep;
            var drag = _placedTotem.GetComponent<DragHandler>();
            _input.CanBeginInteraction = d => ReferenceEquals(d, drag);
            _input.CanEndInteraction = (d, p) => drag.IsRotating;
            drag.RotationOnly = true;
            _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.HoldDrag, World(_placedTotem.CurrentCell));
            try { await Until(() => _placedTotem.RotationStep != rotation, token); }
            finally { if (drag != null) drag.RotationOnly = false; }
        }

        private async UniTask PreviewChoiceAsync(CancellationToken token)
        {
            var cards = _levelUpUI.ChoiceArea.GetComponentsInChildren<LevelUpCardUI>();
            var targets = new System.Collections.Generic.List<UnitBase>();
            var card = cards.First(c => c.GetData() == _settings.LevelUpChoices[0]);
            if (LevelUpFeedbackTargets.Resolve(card.GetData(), _grid, targets) != LevelUpFeedbackDestination.Units || !targets.Contains(_mergedUnit))
                throw new InvalidOperationException("Tutorial preview must highlight the fixed merged unit.");
            bool previewed = false;
            bool released = false;
            void Peek(LevelUpCardUI source, bool peeking)
            {
                if (peeking) { previewed = true; _overlay.Hide(); }
                else if (previewed) released = true;
            }
            foreach (var choice in cards) { choice.AllowSelection = false; choice.SetTutorialPreviewOnly(true); }
            card.OnPeekChanged += Peek;
            _dialogue.ShowText(_settings.ChoicePreviewInstruction, _lessonIndex, _lessonCount, false);
            _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Hold, Ui((RectTransform)card.transform));
            try
            {
                await Until(() => released && !_levelUpUI.FieldPreviewBlocksInput, token);
                foreach (var choice in cards) choice.SetTutorialPreviewOnly(false);
                Canvas.ForceUpdateCanvases();
                card.DescriptionText.ForceMeshUpdate();
                if (card.DescriptionText.textInfo.linkCount == 0)
                    throw new InvalidOperationException("The tutorial choice requires a linked description term.");
                _dialogue.ShowText(_settings.ChoiceTermInstruction, _lessonIndex, _lessonCount, false);
                _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap,
                    () => ChoiceTermBounds(card.DescriptionText));
                await Until(() => _levelUpUI.DescriptionBlocksInput, token);
                _overlay.Hide();
                _dialogue.Hide();
                await Until(() => !_levelUpUI.DescriptionBlocksInput, token);
                _dialogue.ShowText(_settings.ChoiceSelectInstruction, _lessonIndex, _lessonCount, false);
            }
            finally
            {
                if (card != null) card.OnPeekChanged -= Peek;
                foreach (var choice in cards) if (choice != null) { choice.AllowSelection = true; choice.SetTutorialPreviewOnly(false); }
            }
        }

        private static Rect ChoiceTermBounds(TMPro.TMP_Text text)
        {
            var link = text.textInfo.linkInfo[0];
            var canvas = text.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = link.linkTextfirstCharacterIndex; i < link.linkTextfirstCharacterIndex + link.linkTextLength; i++)
            {
                var character = text.textInfo.characterInfo[i];
                var a = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(character.bottomLeft));
                var b = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(character.topRight));
                min = Vector2.Min(min, a); max = Vector2.Max(max, b);
            }
            return Rect.MinMaxRect(min.x - 8, min.y - 8, max.x + 8, max.y + 8);
        }

        private async UniTask AwarenessAsync(Func<Rect> target, CancellationToken token)
        {
            _time.Pause(this);
            _overlay.Show(_settings, false, true, false, IngameTutorialOverlay.Gesture.Tap, target);
            await AcknowledgeAsync(token);
            _dialogue.Hide();
            Block();
            // Do not let the dismissing touch become a world press on the same frame.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        }

        private async UniTask AcknowledgeAsync(CancellationToken token)
        {
            while (true)
            {
                int count = _overlay.TapCount;
                await Until(() => _overlay.TapCount > count, token);
                if (!_dialogue.IsTyping) return;
                _dialogue.CompleteTyping();
            }
        }

        private Func<Rect> ButtonArea(Button button) => () => _overlay.ScreenRect(
            (RectTransform)button.transform, button.targetGraphic != null ? button.targetGraphic.raycastPadding : Vector4.zero);
        private Func<Rect> Ui(RectTransform target) => () => _overlay.ScreenRect(target);
        private Rect RequiredTotemBounds()
        {
            var icon = _overlay.ScreenRect(_rewardUI.RequiredChoiceArea);
            var description = _overlay.ScreenRect(_rewardUI.RequiredChoiceDescriptionArea);
            return Rect.MinMaxRect(Mathf.Min(icon.xMin, description.xMin), Mathf.Min(icon.yMin, description.yMin),
                Mathf.Max(icon.xMax, description.xMax), Mathf.Max(icon.yMax, description.yMax));
        }
        private async UniTask ChooseTotemAsync(CancellationToken token)
        {
            var dialogueRect = (RectTransform)_dialogue.transform;
            var min = dialogueRect.anchorMin; var max = dialogueRect.anchorMax;
            var offsetMin = dialogueRect.offsetMin; var offsetMax = dialogueRect.offsetMax;
            // The required reward is the top band; keep its icon and description uncovered.
            dialogueRect.anchorMin = new Vector2(0.06f, 0.46f);
            dialogueRect.anchorMax = new Vector2(0.94f, 0.61f);
            dialogueRect.offsetMin = dialogueRect.offsetMax = Vector2.zero;
            try
            {
                await Until(() => _rewardUI.IsOpen, token);
                Block();
                await Until(() => _rewardUI.IsReadyForSelection, token);
                ShowRecipeDialogue();
                await AwarenessAsync(Ui(_rewardUI.ChoiceArea), token);
                _dialogue.ShowText(_currentRecipe.Instruction, _lessonIndex, _lessonCount, false);
                _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, RequiredTotemBounds);
                await Until(() => _rewardUI.IsReadyForConfirmation, token);
                _dialogue.ShowText("강조된 버튼을 눌러 토템을 받아 보세요.", _lessonIndex, _lessonCount, false);
                _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, Ui(_rewardUI.ConfirmationArea));
                await Until(() => !_rewardUI.IsOpen && _inventory.Items.Count > 0, token);
                _overlay.Hide(); _time.Release(this);
            }
            finally
            {
                dialogueRect.anchorMin = min; dialogueRect.anchorMax = max;
                dialogueRect.offsetMin = offsetMin; dialogueRect.offsetMax = offsetMax;
            }
        }
        private Rect ScreenBounds(Bounds bounds)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var point = (Vector2)_camera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private Func<Rect> World(GridCell cell) => () =>
        {
            if (cell == null || _camera == null) return Rect.zero;
            if (!cell.TryGetVisualBounds(out var bounds)) bounds = new Bounds(cell.transform.position, Vector3.one);
            return ScreenBounds(bounds);
        };
        private static bool IsCellAt(Vector2 p, GridCell cell)
        {
            foreach (var hit in Physics2D.RaycastAll(p, Vector2.zero))
                if (hit.collider.GetComponent<GridCell>() == cell) return true;
            return false;
        }
        private void Block() => _overlay.Show(_settings, false, false, false, IngameTutorialOverlay.Gesture.Tap);
        private static UniTask Until(Func<bool> predicate, CancellationToken token) => UniTask.WaitUntil(predicate, cancellationToken: token);
        private static UniTask Delay(float seconds, CancellationToken token) => UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: token);
        private static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1 : 0; group.blocksRaycasts = visible; group.interactable = visible;
        }
        private async UniTask RevealAsync(CanvasGroup group, CancellationToken token)
        {
            if (group == null) return;
            await group.DOFade(1, _settings.RevealSeconds).SetUpdate(true).ToUniTask(cancellationToken: token);
            group.blocksRaycasts = true; group.interactable = true;
        }
        private void Restore()
        {
            _income = false;
            if (!_running) return;
            _running = false;
            if (_overlay != null) _overlay.Hide();
            _time?.Release(this);
            _time?.Release(_customPauseOwner);
            if (_dialogue != null) _dialogue.Hide();
            if (_exp != null) _exp.DeferLevelUps = false;
            if (_levelUpUI != null) _levelUpUI.RequireUnitPreview = false;
            if (_input != null) { _input.CanBeginInteraction = null; _input.CanEndInteraction = null; _input.AllowPointerClicks = true; }
            if (_boss != null && _boss.CurrentBoss != null) _boss.CurrentBoss.Invincible = false;
            if (_guidedBoss != null && !_guidedBoss.IsDead && _patterns != null) _patterns.RegisterBoss(_guidedBoss, _guidedBoss.Patterns);
            foreach (var group in _bossHud) SetVisible(group, true);
            SetVisible(_upgradeGroup,true); SetVisible(_chiefGroup,true); SetVisible(_inventoryGroup,true);
            SetVisible(_summonGroup, true); SetVisible(_currencyGroup, true);
            if (_hasCameraPose && _camera != null) { _camera.transform.position = _cameraPosition; _camera.orthographicSize = _cameraSize; _camera.fieldOfView = _cameraFov; }
        }
        private void OnDisable() { _cts?.Cancel(); Restore(); }
    }
}
