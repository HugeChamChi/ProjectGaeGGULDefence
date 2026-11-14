using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using VContainer;

namespace GaeGGUL.Tutorial
{
    public sealed partial class IngameTutorialDirector
    {
        [Inject] private TutorialWaveFlow _tutorialFlow;
        [Inject] private ConversionService _conversion;
        [Inject] private ConversionStoneWallet _stones;
        private BossDeertree _deer;
        private int _combatHits;

        private void PrepareLessonBoss(BossBase boss, int wave)
        {
            if (_guidedBoss != null) _guidedBoss.OnDamaged -= CountCombatHit;
            _guidedBoss = boss;
            _bossExpMultiplier = boss.ExpMultiplier;
            boss.PreventDeath = true;
            boss.OnDamaged += CountCombatHit;
            if (wave == 0) { boss.ExpMultiplier = 0; _patterns.UnregisterBoss(boss); }
            if (wave == 1) _patterns.UnregisterBoss(boss);
            if (wave == 2)
            {
                _deer = boss as BossDeertree;
                if (_deer == null || _deer.RuntimeSettings == null || !_deer.RuntimeSettings.CurseOnlyPractice)
                    throw new InvalidOperationException("WAVE 3 requires the authored curse-only practice deer.");
                boss.LessonHpFloor = _settings.DeerPreparationHpFloor;
            }
        }

        private void CountCombatHit(decimal amount, Vector3? position) => _combatHits++;

        private async UniTask CombatGapAsync(CancellationToken token)
        {
            _dialogue.Hide(); _overlay.Hide(); LockWorldInput();
            _fieldPause.Exit(this);
            _time.Release(this);
            int hits = _combatHits;
            float elapsed = 0;
            while (elapsed < _settings.ObserveCombatSeconds || _combatHits == hits)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (_game.CurrentState == GameManager.GameState.Playing) elapsed += Time.deltaTime;
            }
        }

        private async UniTask SecondWaveAsync(CancellationToken token)
        {
            await CombatGapAsync(token);
            _guidedBoss.PreventDeath = false;
            await Until(() => _wave.CurrentWave == 1 && _guidedBoss != null && !_guidedBoss.IsDead, token);
            _dialogue.ShowText("처치하면 남은 시간이 60초로 돌아와요.", _lessonIndex, _lessonCount, false);
            await Delay(_settings.FocusSeconds, token);
        }

        private async UniTask TelegraphAsync(CancellationToken token)
        {
            _patterns.RegisterBoss(_guidedBoss, new[] { _settings.CounterPattern });
            await Until(() => _patterns.IsCounterablePattern(_guidedBoss), token);
            _dialogue.ShowText("빛나는 칸은 공격 예고예요. 악어의 지진은 모든 칸에 닿아요.", _lessonIndex, _lessonCount, true);
            await AwarenessAsync(() => _grid.AllCells().Select(c => World(c)()).Aggregate((a,b) =>
                Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin), Mathf.Min(a.yMin,b.yMin), Mathf.Max(a.xMax,b.xMax), Mathf.Max(a.yMax,b.yMax))), token);
            await ReleasePointersAsync(token);
            _time.Release(this);
            await Until(() => !_patterns.IsCounterablePattern(_guidedBoss), token);
            await UniTask.Delay(TimeSpan.FromSeconds(_settings.CounterPattern.duration), cancellationToken: token);
            _patterns.UnregisterBoss(_guidedBoss);
            await CombatGapAsync(token);
        }

        private async UniTask FieldViewLessonAsync(CancellationToken token)
        {
            var field = _levelUpUI.FieldView;
            if (field == null) throw new InvalidOperationException("Field-view footer is missing.");
            var cards = _levelUpUI.ChoiceArea.GetComponentsInChildren<LevelUpCardUI>();
            foreach (var card in cards) card.AllowSelection = false;
            try
            {
                _dialogue.ShowText("필드 보기로 배치와 레벨을 확인해 보세요.", _lessonIndex, _lessonCount, false);
                _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, ButtonArea(field.ToggleButton));
                await Until(() => field.ViewingField, token);
                _dialogue.ShowText("확인이 끝나면 선택지로 돌아가 주세요.", _lessonIndex, _lessonCount, false);
                await Until(() => !field.ViewingField && !field.BlocksSelection, token);
            }
            finally { foreach (var card in cards) if (card != null) card.AllowSelection = true; }
        }

        private async UniTask ConversionLessonAsync(CancellationToken token)
        {
            _stones.Add(_settings.PracticeConversionStones);
            _dialogue.ShowText("변환석을 얻었어요. 다음 전투에서 사용해 볼게요.", _lessonIndex, _lessonCount, false);
            _time.Release(this);
            _fieldPause.Exit(this);
            await Until(() => _wave.CurrentWave == 2 && _deer != null, token);
            await CombatGapAsync(token);
            var target = _grid.GetCell(_settings.SpawnCells[2]).OccupyingUnit;
            if (target == null || target.BaseLevel != 1) throw new InvalidOperationException("Practice conversion target is unavailable.");
            var cell = target.currentCell;
            _conversion.TutorialTarget = target;
            _time.Pause(this);
            // A tap on the practice unit selects it and shows its conversion button; other units and drags stay locked.
            var targetDrag = target.GetComponent<DragHandler>();
            _input.CanBeginInteraction = d => ReferenceEquals(d, targetDrag);
            _input.CanEndInteraction = (d, p) => false;
            _input.AllowPointerClicks = true;
            _dialogue.ShowText("감망을 눌러 보세요. 변환석 1개로 같은 기본 레벨의 다른 유닛으로 바꿔요.", _lessonIndex, _lessonCount, false);
            _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, World(cell));
            var button = FindFirstObjectByType<ConversionButtonView>(FindObjectsInactive.Include);
            if (button == null) throw new InvalidOperationException("Conversion button is missing.");
            try
            {
                await Until(() => button.Unit == target && button.gameObject.activeInHierarchy, token);
                _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, Ui((RectTransform)button.transform));
                await Until(() => cell.OccupyingUnit != null && cell.OccupyingUnit != target, token);
                Block(); LockWorldInput();
                await Until(() => cell.OccupyingUnit.gameObject.activeInHierarchy, token);
            }
            finally { _conversion.TutorialTarget = null; if (_running && _input != null) LockWorldInput(); }
            await CombatGapAsync(token);
            _guidedBoss.LessonHpFloor = 0;
        }

        private async UniTask CurseLessonAsync(CancellationToken token)
        {
            _dialogue.Hide(); _overlay.Hide();
            await Until(() => _deer.Encounter != null && _deer.Encounter.CurseActive, token);
            _time.Pause(this); Block();
            _chiefButton.interactable = false;
            int index = Array.FindIndex(_deer.Encounter.Cells, c => c.CurseTapsLeft > 0);
            if (index < 0) throw new InvalidOperationException("Curse practice has no bound target.");
            var cell = _deer.Cells[index];
            if (_camera != null)
            {
                _cameraPosition = _camera.transform.position; _cameraSize = _camera.orthographicSize;
                _cameraFov = _camera.fieldOfView; _hasCameraPose = true;
                var target = cell.transform.position; target.z = _cameraPosition.z;
                await MoveLessonCameraAsync(target, _settings.CameraZoomRatio, token);
            }
            _dialogue.ShowText("저주에 묶인 유닛이에요. 이 유닛을 다섯 번 눌러 풀어 주세요.", _lessonIndex, _lessonCount, true);
            await AwarenessAsync(World(cell), token);
            if (_camera != null) await MoveLessonCameraAsync(_cameraPosition, 1f, token);
            _hasCameraPose = false;
            await ReleasePointersAsync(token);
            _dialogue.ShowText("묶인 유닛을 다섯 번 눌러 주세요. 시간이 끝나도 다시 연습할 수 있어요.", _lessonIndex, _lessonCount, false);
            _overlay.Show(_settings, true, true, true, IngameTutorialOverlay.Gesture.Tap, World(cell));
            _time.Release(this);
            // Direct boss input handles bindings. Keep selection/dragging disabled.
            await Until(() => _deer.Encounter.Cells[index].CurseTapsLeft == 0, token);
            Block();
            _dialogue.ShowText("저주를 풀었어요! 이제 사슴을 물리쳐 보세요.", _lessonIndex, _lessonCount, false);
            await Delay(_settings.FocusSeconds, token);
            await CombatGapAsync(token);
        }

        private async UniTask MoveLessonCameraAsync(Vector3 position, float ratio, CancellationToken token)
        {
            await DOTween.Sequence().SetUpdate(true)
                .Append(_camera.transform.DOMove(position, _settings.CameraZoomSeconds))
                .Join(_camera.orthographic ? _camera.DOOrthoSize(_cameraSize * ratio, _settings.CameraZoomSeconds)
                    : _camera.DOFieldOfView(_cameraFov * ratio, _settings.CameraZoomSeconds))
                .ToUniTask(cancellationToken: token);
            token.ThrowIfCancellationRequested();
        }

        private async UniTask ReleasePointersAsync(CancellationToken token)
        {
            Block();
            await Until(() => Input.touchCount == 0 && !Input.GetMouseButton(0), token);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        }

        private void RestoreThreeWaveLessons()
        {
            _tutorialFlow.OnBossReady -= PrepareLessonBoss;
            if (_guidedBoss != null) { _guidedBoss.OnDamaged -= CountCombatHit; _guidedBoss.LessonHpFloor = 0; }
            if (_conversion != null) _conversion.TutorialTarget = null;
            if (_levelUpUI != null) _levelUpUI.HideFieldViewFooter = false;
            if (_chiefButton != null) _chiefButton.interactable = true;
            if (_camera != null) { _camera.DOKill(); _camera.transform.DOKill(); }
        }
    }
}
