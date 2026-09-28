using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GaeGGUL.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Plays the authored flow through gameplay/UI handlers and checks gating and cleanup.</summary>
public static class IngameTutorialChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Report = "outputs/tutorial-qa/checks.txt";

    /// <summary>Runs only in a fresh TutorialScene Play session; does not write persistent player state.</summary>
    [MenuItem("Tools/USW/Tutorial/Run Play Checks")]
    public static void Run() => RunAsync().Forget();

    private static async UniTaskVoid RunAsync()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter TutorialScene Play Mode first.");
        Directory.CreateDirectory("outputs/tutorial-qa");
        var log = new StringBuilder();
        var director = Object.FindFirstObjectByType<IngameTutorialDirector>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(director.GetCancellationTokenOnDestroy());
        using var timeout = cts.CancelAfterSlim(TimeSpan.FromMinutes(5), DelayType.Realtime);
        var token = cts.Token;
        void Check(bool condition,string description)
        {
            if(!condition) throw new Exception(description);
            log.AppendLine("PASS " + description);File.WriteAllText(Report,log.ToString());
        }
        async UniTask Stage(IngameTutorialStage stage)
        {
            await UniTask.WaitUntil(()=>director.CurrentStage==stage,cancellationToken:token);
            await UniTask.Delay(100,DelayType.Realtime,cancellationToken:token);
        }
        async UniTask Frames() { await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate,token); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate,token); }
        try
        {
            var overlay=Get<IngameTutorialOverlay>(director,"_overlay");
            var dialogue=Get<IngameTutorialDialogue>(director,"_dialogue");
            var spawn=Get<UnitSpawner>(director,"_spawner");
            var input=Get<InputManager>(director,"_input");
            var grid=Get<GridManager>(director,"_grid");
            var game=Get<GameManager>(director,"_game");
            var boss=Get<BossManager>(director,"_boss");
            var settings=Get<IngameTutorialSettings>(director,"_settings");
            var summon=Get<Button>(director,"_summonButton");
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=-1};
            await Stage(IngameTutorialStage.FirstSummon);
            Check(dialogue.gameObject.activeInHierarchy,"Dialogue visible with first forced action");
            Check(!overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)summon.transform).center,null),"Summon target passes overlay");
            Check(overlay.IsRaycastLocationValid(Vector2.one,null),"Outside target is blocked");
            overlay.OnPointerClick(pointer);await Frames();
            Check(director.CurrentStage==IngameTutorialStage.FirstSummon,"Dialogue/background tap cannot skip forced action");
            summon.onClick.Invoke();
            await Stage(IngameTutorialStage.ThreeSummons);
            Check(spawn.SuccessfulSpawnCount==1 && spawn.LastSpawnedUnit.OriginalData==settings.SpawnUnits[0],"First summon is authored attack unit");
            Check(boss.CurrentBoss!=null && boss.CurrentBoss.Invincible,"Boss appears after first unit and is protected during merge lesson");
            for(int i=0;i<3;i++)
            {
                await UniTask.WaitUntil(()=>!overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)summon.transform).center,null),cancellationToken:token);
                int before=spawn.SuccessfulSpawnCount;summon.onClick.Invoke();
                await UniTask.WaitUntil(()=>spawn.SuccessfulSpawnCount==before+1 && spawn.LastSpawnedUnit.gameObject.activeInHierarchy,cancellationToken:token);
                await Frames();
            }
            await Stage(IngameTutorialStage.Merge);
            var first=grid.GetCell(settings.SpawnCells[0]).OccupyingUnit;
            var second=grid.GetCell(settings.SpawnCells[1]).OccupyingUnit;
            Check(UnitMergeRules.CanPair(first,second),"Fixed summon pair can merge");
            var origin=first.currentCell;var target=second.currentCell;
            Drag(input,first.GetComponent<Collider2D>().bounds.center,new Vector2(-100,-100));await Frames();
            Check(origin.OccupyingUnit==first,"Invalid merge drop is rejected without losing unit");
            Drag(input,first.GetComponent<Collider2D>().bounds.center,target.transform.position);
            await Stage(IngameTutorialStage.LevelUp);
            Check(first==null && second==null,"Real drag merge consumes both ingredients");
            var level=Get<LevelUpUI>(director,"_levelUpUI");
            await UniTask.WaitUntil(()=>level.IsReadyForSelection,cancellationToken:token);
            await Frames();
            Check(overlay.IsRaycastLocationValid(overlay.ScreenRect(level.ChoiceArea).center,null),"Awareness intercepts taps on choices");
            overlay.OnPointerClick(pointer);await Frames();
            Check(game.CurrentState==GameManager.GameState.LevelUp,"Dismissal does not auto-select a card");
            var card=level.ChoiceArea.GetComponentsInChildren<LevelUpCardUI>().First(c=>c.GetData().specialEffect!=LevelUpSpecialEffect.RerollChoices);
            card.OnPointerDown(pointer);card.OnPointerUp(pointer);card.OnPointerClick(pointer);
            await Stage(IngameTutorialStage.OpenUpgrade);
            var upgrade=Get<Button>(director,"_upgradeButton");
            await UniTask.WaitUntil(()=>!overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)upgrade.transform).center,null),cancellationToken:token);
            upgrade.onClick.Invoke();await Stage(IngameTutorialStage.UpgradeSlots);
            await UniTask.Delay(700,DelayType.Realtime,cancellationToken:token);
            overlay.OnPointerClick(pointer);await Frames();
            var panel=Get<HSD.UI.Upgrade.UI_UpgradePanel>(director,"_upgradePanel");
            var close=(Button)new SerializedObject(panel).FindProperty("btn_BackgroundClose").objectReferenceValue;
            close.onClick.Invoke();await Stage(IngameTutorialStage.ChiefSkill);
            await UniTask.Delay(TimeSpan.FromSeconds(settings.ChiefDelaySeconds+settings.RevealSeconds+0.2f),DelayType.Realtime,cancellationToken:token);
            overlay.OnPointerClick(pointer);await Stage(IngameTutorialStage.TotemChoice);
            // Advance only combat duration here; all tutorial input/completion uses real handlers.
            boss.CurrentBoss.TakeDamage(boss.CurrentBoss.MaxHp*10);
            var reward=Get<TotemRewardUI>(director,"_rewardUI");
            await UniTask.WaitUntil(()=>reward.IsReadyForSelection,cancellationToken:token);await Frames();
            Check(dialogue.gameObject.activeInHierarchy,"Reward dialogue starts only after reward reveal");
            overlay.OnPointerClick(pointer);await Frames();
            var overview=reward.ChoiceArea.GetComponentsInChildren<Button>().First();overview.onClick.Invoke();
            await UniTask.Delay(1000,DelayType.Realtime,cancellationToken:token);
            var confirm=reward.GetComponentsInChildren<Button>().First(b=>b.name.Contains("Confirm"));confirm.onClick.Invoke();
            await Stage(IngameTutorialStage.OpenInventory);
            var invButton=Get<Button>(director,"_inventoryButton");
            await UniTask.Delay(500,DelayType.Realtime,cancellationToken:token);invButton.onClick.Invoke();
            await Stage(IngameTutorialStage.PlaceTotem);await UniTask.Delay(500,DelayType.Realtime,cancellationToken:token);
            var invUi=Get<TotemInventoryUI>(director,"_inventoryUI");
            var slot=invUi.GetSlotRect(0).GetComponent<TotemInventorySlotUI>();
            var storage=Get<TotemInventory>(director,"_inventory");
            var expectedCell=Get<GridCell>(director,"_dropCell");
            var wrongCell=grid.GetEmptyCells().First(c=>c!=expectedCell);
            Check(!await storage.TryPlaceAsync(0,wrongCell,token) && storage.Items.Count==1,"Wrong tutorial placement preserves stored totem");
            pointer.pressPosition=overlay.ScreenRect(invUi.GetSlotRect(0)).center;
            pointer.position=Vector2.one;
            slot.OnBeginDrag(pointer);slot.OnDrag(pointer);slot.OnEndDrag(pointer);
            await UniTask.Delay(500,DelayType.Realtime,cancellationToken:token);
            Check(invUi.IsOpen && storage.Items.Count==1,"Cancelled inventory drag reopens drawer for retry");
            pointer.pressPosition=overlay.ScreenRect(invUi.GetSlotRect(0)).center;
            var drop=Get<GridCell>(director,"_dropCell");pointer.position=Camera.main.WorldToScreenPoint(drop.transform.position);
            slot.OnBeginDrag(pointer);slot.OnDrag(pointer);slot.OnEndDrag(pointer);
            await Stage(IngameTutorialStage.MoveTotem);
            var totem=Get<TotemBase>(director,"_placedTotem");
            Check(totem.Data.isRotatable,"Chosen reward is rotatable");
            drop=Get<GridCell>(director,"_dropCell");
            Physics2D.SyncTransforms();
            Drag(input,totem.GetComponent<Collider2D>().bounds.center,drop.transform.position);
            await Stage(IngameTutorialStage.RotateTotem);
            Check(totem.CurrentCell==drop,"Immediate drag moves the totem");
            Physics2D.SyncTransforms();
            var start=(Vector2)Camera.main.WorldToScreenPoint(totem.GetComponent<Collider2D>().bounds.center);
            Call(input,"ProcessPointerDown",start);
            await UniTask.Delay(800,DelayType.Realtime,cancellationToken:token);
            var end=(Vector2)Camera.main.WorldToScreenPoint(totem.transform.position)+Vector2.right*150;
            Call(input,"ProcessPointerMove",end);Call(input,"ProcessPointerUp",end);
            await UniTask.WaitUntil(()=>director.IsComplete,cancellationToken:token);
            Check(totem.RotationStep==1,"Hold then drag right rotates the totem");
            Check(input.CanBeginInteraction==null && input.CanEndInteraction==null && input.AllowPointerClicks,"Completion restores normal world input");
            Check(!dialogue.gameObject.activeInHierarchy && !overlay.gameObject.activeInHierarchy,"Dialogue and highlight close together");
            await CheckCompositionAsync(director,Check,token);
            log.AppendLine("COMPLETE — scripted handlers; Android touch and visual timing still require device review.");
        }
        catch(Exception e){log.AppendLine("FAIL "+e);Debug.LogException(e);}
        finally{File.WriteAllText(Report,log.ToString());Debug.Log(log.ToString());}
    }

    private static async UniTask CheckCompositionAsync(IngameTutorialDirector director, Action<bool,string> check, CancellationToken token)
    {
        var plan=Get<IngameTutorialPlan>(director,"_plan");
        var copy=Object.Instantiate(plan);
        var lesson=ScriptableObject.CreateInstance<IngameTutorialLesson>();
        var overlay=Get<IngameTutorialOverlay>(director,"_overlay");
        var dialogue=Get<IngameTutorialDialogue>(director,"_dialogue");
        var time=Get<TimeScaleService>(director,"_time");
        var previousTargets=Get<TutorialTargetBinding[]>(director,"_targets");
        var host=new GameObject("TutorialChecksTarget",typeof(RectTransform),typeof(Button),typeof(TutorialTargetBinding));
        host.transform.SetParent(overlay.transform.parent,false);
        var binding=host.GetComponent<TutorialTargetBinding>();binding.Key="QA";binding.Highlight=(RectTransform)host.transform;binding.Button=host.GetComponent<Button>();
        var targetsField=typeof(IngameTutorialDirector).GetField("_targets",Private);
        targetsField.SetValue(director,new[]{binding});
        UniTask Run(CancellationToken ct) => (UniTask)typeof(IngameTutorialDirector).GetMethod("ExecuteCustomAsync",Private).Invoke(director,new object[]{lesson,ct});
        var otherPause=new object();
        try
        {
            lesson.Kind=IngameTutorialLesson.LessonKind.Awareness;lesson.Instruction="QA awareness";lesson.TargetKey="QA";
            copy.Lessons.Insert(2,lesson);
            check(copy.Validate()==null,"Designer can insert reusable awareness into the 13-step recipe");
            copy.Lessons.RemoveAt(0);
            check(copy.Validate()!=null,"Sequence rejects missing gameplay prerequisites");
            time.Pause(otherPause);
            var awareness=Run(token);await UniTask.WaitUntil(()=>dialogue.gameObject.activeSelf,cancellationToken:token);
            overlay.OnPointerClick(new PointerEventData(EventSystem.current));await awareness;
            check(time.IsRequesting(otherPause) && !dialogue.gameObject.activeSelf,"Awareness cleanup preserves another system's pause");
            lesson.Kind=IngameTutorialLesson.LessonKind.ForceButton;
            var forced=Run(token).Preserve();await UniTask.WaitUntil(()=>dialogue.gameObject.activeSelf,cancellationToken:token);
            overlay.OnPointerClick(new PointerEventData(EventSystem.current));await UniTask.Yield(token);
            check(forced.Status==UniTaskStatus.Pending && dialogue.gameObject.activeSelf,"Custom force-button ignores background taps and keeps dialogue");
            binding.Button.onClick.Invoke();await forced;
            check(!dialogue.gameObject.activeSelf,"Custom force-button completes with its actual target event");
            lesson.Kind=IngameTutorialLesson.LessonKind.WaitForSignal;lesson.PauseGameplay=false;lesson.CompletionSignal="QA.Done";
            director.Signal("QA.Done");
            var signal=Run(token).Preserve();await UniTask.WaitUntil(()=>dialogue.gameObject.activeSelf,cancellationToken:token);
            director.Signal("QA.Wrong");await UniTask.Yield(token);
            check(signal.Status==UniTaskStatus.Pending,"Signal wait ignores stale and unrelated events");
            director.Signal("QA.Done");await signal;
            lesson.Kind=IngameTutorialLesson.LessonKind.ForceButton;lesson.PauseGameplay=true;
            using var cancel=CancellationTokenSource.CreateLinkedTokenSource(token);
            var cancelled=Run(cancel.Token);await UniTask.WaitUntil(()=>dialogue.gameObject.activeSelf,cancellationToken:token);cancel.Cancel();
            try { await cancelled;check(false,"Cancelled guide must not complete successfully"); }
            catch(OperationCanceledException) { }
            check(!overlay.gameObject.activeSelf && !dialogue.gameObject.activeSelf && time.IsRequesting(otherPause),"Cancellation cleans custom UI while preserving unrelated pause");
        }
        finally
        {
            time.Release(otherPause);targetsField.SetValue(director,previousTargets);
            Object.Destroy(host);Object.Destroy(lesson);Object.Destroy(copy);
        }
    }
    private static T Get<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
    private static void Call(object obj,string method,Vector2 p)=>obj.GetType().GetMethod(method,Private).Invoke(obj,new object[]{p});
    private static void Drag(InputManager input,Vector2 from,Vector2 to)
    {
        Physics2D.SyncTransforms();
        Call(input,"ProcessPointerDown",Camera.main.WorldToScreenPoint(from));
        Call(input,"ProcessPointerMove",Camera.main.WorldToScreenPoint(to));
        Call(input,"ProcessPointerUp",Camera.main.WorldToScreenPoint(to));
    }
}
