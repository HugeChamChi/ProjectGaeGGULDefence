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

    [MenuItem("Tools/USW/Tutorial/Run First Boss Balance Check")]
    public static void RunFirstBossBalance() => RunAsync(true).Forget();
    
    private static async UniTaskVoid RunAsync(bool firstBossBalanceOnly = false)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter TutorialScene Play Mode first.");
        Directory.CreateDirectory("outputs/tutorial-qa");
        var log = new StringBuilder();
        string report = firstBossBalanceOnly ? "outputs/tutorial-qa/balance.txt" : Report;
        var director = Object.FindFirstObjectByType<IngameTutorialDirector>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(director.GetCancellationTokenOnDestroy());
        using var timeout = cts.CancelAfterSlim(TimeSpan.FromMinutes(5), DelayType.Realtime);
        var token = cts.Token;
        const string completedKey = "IngameTutorial.Completed.v1";
        bool hadProgress = PlayerPrefs.HasKey(completedKey);
        int previousProgress = PlayerPrefs.GetInt(completedKey, 0);
        void Check(bool condition,string description)
        {
            if(!condition) throw new Exception(description);
            log.AppendLine("PASS " + description);File.WriteAllText(report,log.ToString());
        }
        async UniTask Stage(IngameTutorialStage stage)
        {
            await UniTask.WaitUntil(()=>director.CurrentStage==stage,cancellationToken:token);
            await UniTask.Delay(100,DelayType.Realtime,cancellationToken:token);
        }
        async UniTask Frames() { await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate,token); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate,token); }
        async UniTask FocusReady(IngameTutorialOverlay overlay) => await UniTask.WaitUntil(()=>!overlay.IsTransitioning,cancellationToken:token);
        async UniTask Snapshot(string name)
        {
            ScreenCapture.CaptureScreenshot("outputs/tutorial-qa/"+name+".png");
            await Frames();
        }
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
            var damageNumbers=Object.FindFirstObjectByType<BossDamageNumbers>();
            Check(damageNumbers!=null && Get<object>(damageNumbers,"_cookieView")!=null && Object.FindFirstObjectByType<DamageFloaterManager>().SuppressOutput,"Tutorial uses the production damage renderer without duplicate legacy floaters");
            Check(Object.FindFirstObjectByType<CenterToast>()!=null,"Tutorial includes the production center toast");
            Check(settings.SpawnUnits.Select(u=>u.unitName).SequenceEqual(new[]{"베탕","베탕","감망","젤탕"}),"Hacking deck starts with two Betan, one Gamman and one Zeltan");
            Check(settings.MergeUnit.unitName=="델탕" && settings.UpgradeTarget=="Deltan","Merge and upgrade target Deltan");
            Check(settings.LevelUpChoices.Select(c=>c.chooseId).SequenceEqual(new[]{9107,9108,9109}),"First choices contain only Deltan upgrades");
            var summon=Get<Button>(director,"_summonButton");
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=-1};
            void ClickButton(Button button, Vector2? screenPoint = null)
            {
                pointer.position=screenPoint ?? overlay.ScreenRect((RectTransform)button.transform).center;
                var hits=new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer,hits);
                var target=hits.Count>0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                Check(target==button.gameObject,"Actual UI raycast reaches "+button.name);
                ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler);
            }
            async UniTask Acknowledge()
            {
                await UniTask.WaitUntil(()=>dialogue.gameObject.activeSelf,cancellationToken:token);
                if(dialogue.IsTyping) { overlay.OnPointerClick(pointer);await Frames(); }
                overlay.OnPointerClick(pointer);await Frames();
            }
            await Stage(IngameTutorialStage.TimeLimit);
            Check(!Get<TMPro.TMP_Text>(dialogue,"_progress").gameObject.activeSelf,"Guide shows only the instruction without a numbered header");
            Check(!Object.FindFirstObjectByType<IngameHudMenu>(FindObjectsInactive.Include).gameObject.activeSelf,"Tutorial option button is hidden");
            Check(spawn.SuccessfulSpawnCount==0 && boss.CurrentBoss!=null,"Boss enters before any summons");
            var camera=Camera.main;
            Check(Vector3.Distance(camera.transform.position,Get<Vector3>(director,"_cameraPosition"))<0.001f,"Entrance restores camera position");
            Check(Get<CanvasGroup>(director,"_summonGroup").alpha==0,"Summon is hidden until the time guide is acknowledged");
            Check(Time.timeScale==0,"Time guide pauses the battle timer");
            var label=Get<TMPro.TMP_Text>(dialogue,"_instruction");
            int visible=label.maxVisibleCharacters;
            await UniTask.Delay(200,DelayType.Realtime,cancellationToken:token);
            Check(!dialogue.IsTyping || label.maxVisibleCharacters>visible,"Typewriter advances while gameplay is paused");
            if(dialogue.IsTyping)
            {
                overlay.OnPointerClick(pointer);await Frames();
                Check(!dialogue.IsTyping && director.CurrentStage==IngameTutorialStage.TimeLimit,"First tap reveals text without skipping the lesson");
            }
            await FocusReady(overlay);await Snapshot("01-time-limit-bubble");
            await Acknowledge();
            await Stage(IngameTutorialStage.FirstSummon);
            await UniTask.WaitUntil(()=>dialogue.gameObject.activeSelf,cancellationToken:token);
            overlay.OnPointerClick(pointer);await Frames();
            Check(director.CurrentStage==IngameTutorialStage.FirstSummon,"Background taps cannot skip forced summons");
            await FocusReady(overlay);await Snapshot("00-first-summon");
            float summonFocusStarted=Get<float>(overlay,"_started");
            for(int i=0;i<settings.SpawnUnits.Length;i++)
            {
                await UniTask.WaitUntil(()=>!overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)summon.transform).center,null),cancellationToken:token);
                Check(dialogue.gameObject.activeInHierarchy,"Summon dialogue stays visible for tap "+(i+1));
                Check(Time.timeScale==0 && Get<FieldPauseVisuals>(director,"_fieldPause").AttacksHeld,"Summons freeze combat while their visuals continue");
                int before=spawn.SuccessfulSpawnCount;ClickButton(summon);
                await UniTask.WaitUntil(()=>spawn.SuccessfulSpawnCount==before+1 && spawn.LastSpawnedUnit.gameObject.activeInHierarchy,cancellationToken:token);
                if(i<settings.SpawnUnits.Length-1)
                    Check(Get<float>(overlay,"_started")==summonFocusStarted && Get<bool>(overlay,"_dim"),"Summon spotlight remains without restarting between taps");
            }
            await Stage(IngameTutorialStage.ObserveCombat);
            Check(spawn.SuccessfulSpawnCount==4,"All four authored summons were purchased");
            Check(Get<CurrencyManager>(director,"_currency").Currency<spawn.CurrentCost,"Available summon budget is exhausted");
            Check(!boss.CurrentBoss.Invincible && boss.CurrentBoss.PreventDeath,"Boss survives mandatory lessons");
            async UniTask SpamUntil(IngameTutorialStage stage)
            {
                int before=spawn.SuccessfulSpawnCount;
                var wallet=Get<CurrencyManager>(director,"_currency");
                float balance=wallet.Currency;
                wallet.AddCurrency(spawn.CurrentCost * 20f);
                float added=wallet.Currency-balance;
                int taps=0;
                while(director.CurrentStage!=stage)
                {
                    float funded=wallet.Currency;
                    for(int tap=0;tap<10;tap++) { summon.onClick.Invoke(); taps++; }
                    if(spawn.SuccessfulSpawnCount!=before || wallet.Currency!=funded) throw new Exception("Transition spam summoned or spent food");
                    await UniTask.Yield(PlayerLoopTiming.Update,token);
                }
                wallet.Spend(added);
                Check(taps>0,"Transition spam blocked "+taps+" funded summon requests before "+stage);
            }
            float observedAt=Time.unscaledTime;
            await SpamUntil(IngameTutorialStage.Merge);
            await Stage(IngameTutorialStage.Merge);
            Check(Time.unscaledTime-observedAt>=settings.ObserveCombatSeconds-0.2f,"Combat observation lasts five seconds");
            await FocusReady(overlay);await Frames();
            var first=grid.GetCell(settings.SpawnCells[0]).OccupyingUnit;
            var second=grid.GetCell(settings.SpawnCells[1]).OccupyingUnit;
            Check(UnitMergeRules.CanPair(first,second),"Fixed summon pair can merge");
            var origin=first.currentCell;var target=second.currentCell;
            Check(Get<System.Collections.Generic.List<Func<Rect>>>(overlay,"_targets").Count==2,"Merge spotlights exactly two units");
            Check(grid.GetOccupiedCells().Where(c=>c.OccupyingUnit!=null && c.OccupyingUnit!=first && c.OccupyingUnit!=second)
                .All(c=>overlay.IsRaycastLocationValid(camera.WorldToScreenPoint(c.OccupyingUnit.GetComponent<Collider2D>().bounds.center),null)),"Other units stay outside merge spotlight");
            await Snapshot("03-merge-two-units");
            var uiRaycasters=Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);
            Check(uiRaycasters.All(r=>!r.isActiveAndEnabled || overlay.transform.IsChildOf(r.transform)),"Merge blocks other UI raycasters");
            Check(!input.AllowPointerClicks && grid.GetOccupiedCells().Where(c=>c.OccupyingUnit!=null && c.OccupyingUnit!=first && c.OccupyingUnit!=second)
                .All(c=>!input.CanBeginInteraction(c.OccupyingUnit.GetComponent<DragHandler>())),"Merge blocks object clicks and unrelated units");
            var isOverUi=typeof(InputManager).GetMethod("IsOverUI",Private);
            var firstScreen=(Vector2)Camera.main.WorldToScreenPoint(first.GetComponent<Collider2D>().bounds.center);
            Check(!(bool)isOverUi.Invoke(input,new object[]{firstScreen,-1}),"Merge unit passes actual UI input filtering");
            Drag(input,first.GetComponent<Collider2D>().bounds.center,new Vector2(-100,-100));await Frames();
            Check(origin.OccupyingUnit==first,"Invalid merge drop is rejected without losing unit");
            Drag(input,first.GetComponent<Collider2D>().bounds.center,target.transform.position);
            await SpamUntil(IngameTutorialStage.OpenUpgrade);
            await Stage(IngameTutorialStage.OpenUpgrade);
            Check(first==null && second==null,"Real merge consumes both ingredients");
            var merged=Get<UnitBase>(director,"_mergedUnit");
            Check(merged.OriginalData==settings.MergeUnit && merged.OriginalTier==Tier.Rare,"Merge produces the fixed rare tutorial unit");
            var upgrade=Get<Button>(director,"_upgradeButton");
            await UniTask.WaitUntil(()=>!overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)upgrade.transform).center,null),cancellationToken:token);
            ClickButton(upgrade);await Stage(IngameTutorialStage.UpgradeSlots);
            await UniTask.Delay(800,DelayType.Realtime,cancellationToken:token);
            await FocusReady(overlay);
            var item=Get<RectTransform>(director,"_upgradeSlots").GetComponentsInChildren<HSD.UI.Upgrade.UI_UpgradeItem>().First(i=>i.UpgradeTarget==settings.UpgradeTarget && i.CanUpgrade);
            Check(!Get<TMPro.TextMeshProUGUI>(item,"txt_Name").gameObject.activeSelf,"Upgrade cards hide unit names");
            int previousLevel=item.CurrentLevel;
            overlay.OnPointerClick(pointer);await Frames();
            Check(director.CurrentStage==IngameTutorialStage.UpgradeSlots,"Background tap cannot skip actual upgrade");
            var upgradeBuy=(Button)new SerializedObject(item).FindProperty("btn_Upgrade").objectReferenceValue;
            ClickButton(upgradeBuy);
            await Frames();
            var statPanel = Object.FindFirstObjectByType<GaeGGUL.UI.Unit.UI_UnitInfoPanel>();
            Check(statPanel == null || !Get<bool>(statPanel,"_isShowing"),
                "Paid upgrade does not open the unit information panel");
            dialogue.CompleteTyping();
            await UniTask.Delay(450,DelayType.Realtime,cancellationToken:token);
            await Snapshot("07-upgrade-card-feedback");
            await Acknowledge();
            await Stage(IngameTutorialStage.ExperienceGauge);
            Check(item.CurrentLevel>previousLevel,"Paid upgrade actually increases its level");
            upgrade.OnPointerClick(pointer);
            Check(!upgrade.IsInteractable() && !Get<HSD.UI.Upgrade.UI_UpgradePanel>(director,"_upgradePanel").IsOpen,
                "Experience tutorial blocks opening the upgrade panel");
            await Acknowledge();
            await UniTask.Delay(900,DelayType.Realtime,cancellationToken:token);
            var exp=Get<ExpManager>(director,"_exp");
            Check(exp.CurrentExp>0 && exp.CurrentExp<exp.ExpToLevelUp,"Experience visibly fills before opening choices");
            await Snapshot("04-experience-fill");
            await Stage(IngameTutorialStage.LevelUp);
            Check(first==null && second==null,"Real drag merge consumes both ingredients");
            var level=Get<LevelUpUI>(director,"_levelUpUI");
            await UniTask.WaitUntil(()=>level.IsReadyForSelection,cancellationToken:token);
            await Frames();
            Check(overlay.IsRaycastLocationValid(overlay.ScreenRect(level.ChoiceArea).center,null),"Awareness intercepts taps on choices");
            await Acknowledge();
            Check(game.CurrentState==GameManager.GameState.LevelUp,"Dismissal does not auto-select a card");
            var cards=level.ChoiceArea.GetComponentsInChildren<LevelUpCardUI>();
            Check(cards.Select(c=>c.GetData()).SequenceEqual(settings.LevelUpChoices),"Choice cards use the fixed authored order");
            await UniTask.WaitUntil(()=>cards.All(c=>!c.AllowSelection),cancellationToken:token);
            await FocusReady(overlay);
            dialogue.CompleteTyping();
            await UniTask.WaitUntil(()=>
            {
                float elapsed=Mathf.Max(0,Time.unscaledTime-Get<float>(overlay,"_started")-settings.FocusSeconds)%settings.GestureSeconds;
                return elapsed>0.35f && elapsed<settings.HoldGestureSeconds;
            },cancellationToken:token);
            await Frames();
            var cueMesh=overlay.canvasRenderer.GetMesh();
            {
                var cueVertices=cueMesh.vertices.Skip(Mathf.Max(0,cueMesh.vertexCount-192)).ToArray();
                var cuePoints=cueVertices.Select(v=>RectTransformUtility.WorldToScreenPoint(null,overlay.rectTransform.TransformPoint(v))).ToArray();
                var cueRect=Rect.MinMaxRect(cuePoints.Min(p=>p.x),cuePoints.Min(p=>p.y),cuePoints.Max(p=>p.x),cuePoints.Max(p=>p.y));
                var handRect=overlay.ScreenRect(Get<Image>(overlay,"_handImage").rectTransform);
                Check(cueVertices.Length==192 && !cueRect.Overlaps(handRect) && cueRect.xMin>=0 && cueRect.yMin>=0 && cueRect.xMax<=Screen.width && cueRect.yMax<=Screen.height,"Complete hold progress ring stays on screen and clear of the hand pointer");
                Check(Vector2.Distance(cueRect.center,handRect.center)<Screen.width*0.25f,"Hold progress stays close to the pointer rather than in a screen corner");
            }
            Check(settings.ChoicePreviewInstruction.Contains("#FF9D32"),"Hold instruction uses authored orange emphasis");
            await Snapshot("04a-hold-preview-guide");
            var previewTargets=new System.Collections.Generic.List<UnitBase>();
            var card=cards.FirstOrDefault(c=>LevelUpFeedbackTargets.Resolve(c.GetData(),grid,previewTargets)==LevelUpFeedbackDestination.Units)
                ?? cards.First(c=>c.GetData().specialEffect!=LevelUpSpecialEffect.RerollChoices);
            card.OnPointerDown(pointer);card.OnPointerUp(pointer);card.OnPointerClick(pointer);await Frames();
            Check(game.CurrentState==GameManager.GameState.LevelUp && !card.AllowSelection,"Quick tap cannot skip required card hold preview");
            card.OnPointerDown(pointer);
            await UniTask.Delay(650,DelayType.Realtime,cancellationToken:token);
            Check(Get<bool>(card,"_peeking") && !overlay.gameObject.activeSelf,"Hold reveals field without tutorial mask hiding units");
            var highlighter=level.GetComponent<LevelUpPeekHighlighter>();
            Check(Get<System.Collections.Generic.List<UnitBase>>(highlighter,"_targets").Count>0,"Held choice highlights its affected field units");
            Check(Get<System.Collections.Generic.List<UnitBase>>(highlighter,"_targets").Contains(merged),"Holding the fixed first card highlights the merged unit");
            upgrade.OnPointerClick(pointer);
            Check(!upgrade.IsInteractable() && !Get<HSD.UI.Upgrade.UI_UpgradePanel>(director,"_upgradePanel").IsOpen,
                "Choice field preview blocks upgrades even while the tutorial overlay is hidden");
            await Snapshot("04-held-card-unit-highlight");
            card.OnPointerUp(pointer);card.OnPointerClick(pointer);
            await UniTask.WaitUntil(()=>!card.TutorialPreviewOnly && !level.FieldPreviewBlocksInput,cancellationToken:token);
            Check(game.CurrentState==GameManager.GameState.LevelUp,"Releasing preview does not select card");
            Check(cards.All(c=>!c.AllowSelection),"Reward selection stays blocked until the term tutorial finishes");
            card.OnPointerDown(pointer);card.OnPointerUp(pointer);card.OnPointerClick(pointer);await Frames();
            Check(game.CurrentState==GameManager.GameState.LevelUp,"A card tap cannot skip the term tutorial");
            await FocusReady(overlay);
            var termText=card.DescriptionText;termText.ForceMeshUpdate();
            var character=termText.textInfo.characterInfo[termText.textInfo.linkInfo[0].linkTextfirstCharacterIndex];
            var termCanvas=termText.GetComponentInParent<Canvas>();
            var termCamera=termCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:termCanvas.worldCamera;
            pointer.position=RectTransformUtility.WorldToScreenPoint(termCamera,termText.transform.TransformPoint((character.bottomLeft+character.topRight)*0.5f));
            var termHits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,termHits);
            var termTarget=termHits.Count>0?ExecuteEvents.GetEventHandler<IPointerClickHandler>(termHits[0].gameObject):null;
            Check(termTarget==card.gameObject,"Tutorial spotlight lets the actual term click reach the card");
            pointer.pointerCurrentRaycast=pointer.pointerPressRaycast=termHits[0];
            dialogue.CompleteTyping();await Snapshot("05-choice-term-guide");
            ExecuteEvents.Execute(termTarget,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(termTarget,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(termTarget,pointer,ExecuteEvents.pointerClickHandler);
            await UniTask.WaitUntil(()=>level.DescriptionBlocksInput,cancellationToken:token);
            await UniTask.Delay(400,DelayType.Realtime,cancellationToken:token);
            Check(cards.All(c=>!c.AllowSelection) && overlay.gameObject.activeSelf && !overlay.raycastTarget && Get<bool>(overlay,"_hand") && level.DescriptionVideoArea!=null,"Term video shows a tapping pointer while keeping modal input and selection locks");
            upgrade.OnPointerClick(pointer);
            Check(!upgrade.IsInteractable() && !Get<HSD.UI.Upgrade.UI_UpgradePanel>(director,"_upgradePanel").IsOpen,
                "Choice term explanation blocks opening the upgrade panel");
            await Snapshot("06-choice-term-popup");
            var termPopup=Object.FindFirstObjectByType<TermPopup>();
            Check(termPopup!=null && termPopup.Film.Rect.gameObject.activeInHierarchy,"Authored hacking term displays its video");
            ClickButton(Get<GameObject>(termPopup,"_shield").GetComponent<Button>(),overlay.ScreenRect(level.DescriptionVideoArea).center);
            Check(!card.AllowSelection,"Tooltip close animation must finish before selection unlocks");
            await UniTask.WaitUntil(()=>!level.DescriptionBlocksInput,cancellationToken:token);
            float closedAt=Time.unscaledTime;
            await UniTask.Delay(900,DelayType.Realtime,cancellationToken:token);
            Check(!card.AllowSelection && !dialogue.gameObject.activeSelf && !Get<bool>(overlay,"_hand"),"Term return stays quiet without a pointer or selection during delay");
            await UniTask.WaitUntil(()=>card.AllowSelection,cancellationToken:token);
            Check(Time.unscaledTime-closedAt>=settings.ChoiceReturnPointDelaySeconds-0.1f && dialogue.gameObject.activeSelf && Get<TMPro.TMP_Text>(dialogue,"_instruction").text==settings.ChoiceSelectInstruction,"Term return waits then restores the Neng selection instruction and pointer");
            dialogue.CompleteTyping();await Snapshot("06b-choice-select-guide");
            Check(game.CurrentState==GameManager.GameState.LevelUp,"Closing the tooltip returns to the choices without selecting one");
            Check(card.GetData().chooseName == "넹?" && cards.Count(c=>c.AllowSelection) == 1,
                "Tutorial selection permits only the Neng-question card");
            Check(cards.All(c=>!c.TermLinksEnabled),"Term links stay disabled during the required card selection");
            var otherChoice = cards.First(c=>c != card);
            otherChoice.OnPointerDown(pointer);otherChoice.OnPointerUp(pointer);otherChoice.OnPointerClick(pointer);
            await Frames();
            Check(game.CurrentState==GameManager.GameState.LevelUp,"Other choice clicks cannot bypass the required tutorial reward");
            await FocusReady(overlay);
            termHits.Clear();EventSystem.current.RaycastAll(pointer,termHits);
            termTarget=termHits.Count>0?ExecuteEvents.GetEventHandler<IPointerClickHandler>(termHits[0].gameObject):null;
            Check(termTarget==card.gameObject,"The final term tap reaches the required card");
            pointer.pointerCurrentRaycast=pointer.pointerPressRaycast=termHits[0];
            ExecuteEvents.Execute(termTarget,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(termTarget,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(termTarget,pointer,ExecuteEvents.pointerClickHandler);
            Check(!level.DescriptionBlocksInput,"Tapping a term selects the required card without reopening its explanation");
            await Stage(IngameTutorialStage.ChiefSkill);
            Check(Get<GameObject>(director,"_optionsButton").activeSelf,"Options appear after reaching the level-up choices");
            Check(upgrade.IsInteractable(),"Finishing choices restores the upgrade button");
            var patterns=Get<BossPatternController>(director,"_patterns");
            await UniTask.WaitUntil(()=>patterns.IsCounterablePattern(boss.CurrentBoss),cancellationToken:token);
            Check(!boss.CurrentBoss.Invincible && boss.CurrentBoss.PreventDeath,"Boss survives until the chief counter lesson finishes");
            await UniTask.Delay(2200,DelayType.Realtime,cancellationToken:token);
            await Acknowledge();
            Check(patterns.IsCounterablePattern(boss.CurrentBoss),"Earthquake warning stays counterable while reading instructions");
            Check(grid.AllCells().Any(c=>c.Model.IsBossTelegraphPreviewed),"Earthquake warning is shown on the field");
            var chief=Get<Button>(director,"_chiefButton");
            await UniTask.WaitUntil(()=>chief.interactable && !overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)chief.transform).center,null),cancellationToken:token);
            var measuredBoss = boss.CurrentBoss;
            var counterHp = measuredBoss.CurrentHp;
            float counterTime = Time.realtimeSinceStartup;
            float counterGameTime = Time.time;
            ClickButton(chief);
            var portrait=chief.GetComponentInChildren<ChiefSkillSpinePortrait>(true);
            var motion=chief.GetComponentInChildren<ChiefSkillIconMotion>(true);
            Check(portrait!=null && portrait.IsPlayingSkill,"Actual chief activation starts tracked skill motion");
            await Frames();
            Check(Get<bool>(motion,"_charged") && !chief.interactable,"Skill motion stays outside box while cooldown disables button");
            await UniTask.WaitUntil(()=>!portrait.IsPlayingSkill,cancellationToken:token);
            await Frames();
            Check(!Get<bool>(motion,"_charged"),"Chief retracts only after skill motion completes");
            Check(!patterns.IsCounterablePattern(boss.CurrentBoss),"Actual chief button cancels the earthquake before impact");
            Check(grid.AllCells().All(c=>!c.Model.IsBossTelegraphPreviewed),"Successful counter clears the field warning");
            if (firstBossBalanceOnly)
            {
                await UniTask.WaitUntil(() => measuredBoss == null || measuredBoss.IsDead ||
                    Time.realtimeSinceStartup - counterTime > 45f, cancellationToken: token);
                float elapsed = Time.realtimeSinceStartup - counterTime;
                log.AppendLine($"MEASURE hp_at_counter={counterHp} real_seconds={elapsed:F3} game_seconds={Time.time-counterGameTime:F3} dead={measuredBoss == null || measuredBoss.IsDead}");
                Check(measuredBoss == null || measuredBoss.IsDead, "First boss dies after chief counter");
                Check(elapsed <= 11f, "First boss dies within approximately 10 seconds of chief button");
                return;
            }
            await Stage(IngameTutorialStage.TotemChoice);
            await UniTask.WaitUntil(()=>!overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)summon.transform).center,null),cancellationToken:token);
            var summonWallet=Get<CurrencyManager>(director,"_currency");
            summonWallet.AddCurrency(Mathf.Max(0,spawn.CurrentCost-summonWallet.Currency));
            int beforeFreeSummon=spawn.SuccessfulSpawnCount;
            ClickButton(summon);
            await UniTask.WaitUntil(()=>spawn.SuccessfulSpawnCount==beforeFreeSummon+1,cancellationToken:token);
            Check(Get<int>(director,"_summonLimit")==int.MaxValue,"Summoning unlocks immediately after the chief lesson, before the totem reward");
            Check(boss.CurrentBoss == null || (!boss.CurrentBoss.Invincible && !boss.CurrentBoss.PreventDeath),
                "Boss can be defeated after reward learning");
            var reward=Get<TotemRewardUI>(director,"_rewardUI");
            await UniTask.WaitUntil(()=>reward.IsReadyForSelection,cancellationToken:token);await Frames();
            Check(Get<WaveManager>(director,"_wave").CurrentWave==1,"Totem guide opens immediately after the second boss reward");
            Check(!input.AllowPointerClicks,"World input stays locked while teaching rewards");
            Check(dialogue.gameObject.activeInHierarchy,"Reward dialogue starts only after reward reveal");
            Check(Get<System.Collections.Generic.List<TotemData>>(reward,"_choices").SequenceEqual(settings.TotemChoices),"Totem reward shows the authored tutorial pool");
            Check(settings.TotemChoices.Length==3 && settings.TotemChoices.Distinct().Count()==3 && settings.RequiredTotem.isRotatable && settings.RequiredTotem.GetSimpleAmount(StatKind.Speed)>0,"Three distinct totems are offered with a required attack-speed reward");
            Call(reward,"OnBandClicked",1);await Frames();
            Check(reward.IsReadyForSelection,"Other totem clicks cannot bypass the required tutorial selection");
            await FocusReady(overlay);
            var forcedTargets=Get<System.Collections.Generic.List<Func<Rect>>>(overlay,"_targets");
            var forcedRect=forcedTargets.Single()();
            Check(forcedRect.height<overlay.ScreenRect(reward.ChoiceArea).height*0.5f,"Forced totem spotlight covers only its content rather than the fullscreen band root");
            Check(dialogue.gameObject.activeInHierarchy && !forcedRect.Overlaps(overlay.ScreenRect((RectTransform)dialogue.transform)),"Forced totem instruction stays visible without covering the required reward");
            dialogue.CompleteTyping();await Snapshot("08-forced-totem-choice");
            var overview=reward.ChoiceArea.GetComponentsInChildren<Button>().First();
            ClickButton(overview,overlay.ScreenRect(reward.RequiredChoiceArea).center);
            await UniTask.Delay(1000,DelayType.Realtime,cancellationToken:token);
            var swipe = Get<TotemRewardSwipeArea>(reward,"_swipeArea");
            async UniTask SwipeDetail(int direction)
            {
                await FocusReady(overlay);
                pointer.pressPosition=overlay.ScreenRect(reward.DetailSwipeArea).center;
                pointer.position=pointer.pressPosition;
                var hits=new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
                Check(hits.Count>0 && ExecuteEvents.GetEventHandler<IEndDragHandler>(hits[0].gameObject)==swipe.gameObject,"Actual UI raycast reaches the totem swipe receiver");
                pointer.position=pointer.pressPosition + Vector2.left*direction*200f;
                swipe.OnBeginDrag(pointer);swipe.OnDrag(pointer);swipe.OnEndDrag(pointer);
                await UniTask.Delay(500,DelayType.Realtime,cancellationToken:token);
            }
            typeof(TotemRewardUI).GetMethod("OnConfirmClicked",Private).Invoke(reward,null);
            Check(reward.IsOpen && !reward.AllowTutorialConfirmation,"Early confirmation is rejected by the reward handler");
            await SwipeDetail(1);
            Check(reward.DetailChoice!=null && reward.DetailChoice!=settings.RequiredTotem,"Real detail swipe reveals another tutorial totem");
            Check(!reward.AllowTutorialConfirmation,"Browsing cannot confirm a reward early");
            await SwipeDetail(-1);
            Check(reward.DetailChoice==settings.RequiredTotem && reward.DetailRotationBadge!=null,"Returning to acceleration shows its actual rotation badge");
            await Acknowledge();
            await UniTask.WaitUntil(()=>Get<TotemRotateBadgeView>(director,"_rotationComparison")!=null,cancellationToken:token);
            await FocusReady(overlay);
            var comparison=Get<TotemRotateBadgeView>(director,"_rotationComparison");
            var crossedRect=overlay.ScreenRect((RectTransform)comparison.transform);
            var normalRect=overlay.ScreenRect(reward.DetailRotationBadge);
            Check(comparison.transform.Find("Slash").gameObject.activeInHierarchy && settings.NonRotatableExample!=null && !settings.NonRotatableExample.isRotatable,"Crossed comparison uses a real non-rotatable totem and its shared badge artwork");
            Check(crossedRect.xMax<normalRect.xMin && Mathf.Abs(crossedRect.center.y-normalRect.center.y)<1f,"Crossed badge is beside the actual rotation icon without overlap");
            var comparisonFocus=Get<System.Collections.Generic.List<Func<Rect>>>(overlay,"_targets").Single()();
            Check(comparisonFocus==crossedRect && !reward.AllowTutorialConfirmation && !reward.AllowTutorialBack && !input.AllowPointerClicks,"Non-rotation explanation focuses its crossed icon and blocks early selection and gameplay");
            dialogue.CompleteTyping();await Snapshot("09-crossed-rotation-badge");
            await Acknowledge();
            await Stage(IngameTutorialStage.OpenInventory);
            Check(Get<TotemInventory>(director,"_inventory").Items.Count==1 && !reward.IsOpen && !reward.AllowTutorialConfirmation && Get<TotemRotateBadgeView>(director,"_rotationComparison")==null,"Explanation automatically confirms exactly one acceleration totem without returning or reselecting");
            var invButton=Get<Button>(director,"_inventoryButton");
            await UniTask.WaitUntil(()=>!overlay.IsRaycastLocationValid(overlay.ScreenRect((RectTransform)invButton.transform).center,null),cancellationToken:token);ClickButton(invButton);
            await Stage(IngameTutorialStage.PlaceTotem);await UniTask.Delay(500,DelayType.Realtime,cancellationToken:token);
            var invUi=Get<TotemInventoryUI>(director,"_inventoryUI");
            var slot=invUi.GetSlotRect(0).GetComponent<TotemInventorySlotUI>();
            var storage=Get<TotemInventory>(director,"_inventory");
            var expectedCell=Get<GridCell>(director,"_dropCell");
            await FocusReady(overlay);
            var drawerPanel=Get<GameObject>(invUi,"_panel");
            var drawerPosition=drawerPanel.transform.localPosition;
            Get<Button>(invUi,"_backgroundCloseButton").onClick.Invoke();await Frames();
            Check(invUi.IsOpen && !Get<bool>(invUi,"_closing") && drawerPanel.transform.localPosition==drawerPosition,"Field tap keeps tutorial inventory stationary and open");
            await Snapshot("05-totem-place");
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
            await FocusReady(overlay);
            var movingTotem=Get<TotemBase>(director,"_placedTotem");
            var moveDestination=Get<GridCell>(director,"_dropCell");
            Check(movingTotem.CurrentCell==expectedCell,"Movement is taught immediately after placement");
            Drag(input,movingTotem.transform.position,moveDestination.transform.position);
            await Stage(IngameTutorialStage.RotateTotem);
            await FocusReady(overlay);await Frames();
            var totem=Get<TotemBase>(director,"_placedTotem");
            var effectBefore=totem.Data.GetEffectCells(totem,grid).ToArray();
            Check(totem.Data.GetSimpleAmount(StatKind.Speed)==0.1f && effectBefore.Length>0 && effectBefore.All(c=>c.HasSpeedBuff),"Placed tutorial totem applies attack speed to its actual cells");
            Check(totem.Data.isRotatable && totem.CurrentCell==moveDestination,"Rotation follows successful totem movement");
            Check(Get<System.Collections.Generic.List<Func<Rect>>>(overlay,"_targets").Count==1,"Rotation highlights only the installed totem");
            var rotationDrag=totem.GetComponent<DragHandler>();
            var originalPosition=totem.transform.position;
            rotationDrag.BeginPress();rotationDrag.OnBeginDrag();rotationDrag.OnDrag((Vector2)originalPosition+Vector2.right);
            Check(totem.transform.position==originalPosition && !rotationDrag.IsDragging,"Rotation lesson prevents immediate movement");
            rotationDrag.EndPress();
            await Snapshot("07-totem-rotate");
            Physics2D.SyncTransforms();
            var start=(Vector2)Camera.main.WorldToScreenPoint(totem.GetComponent<Collider2D>().bounds.center);
            Call(input,"ProcessPointerDown",start);
            await UniTask.Delay(800,DelayType.Realtime,cancellationToken:token);
            var end=(Vector2)Camera.main.WorldToScreenPoint(totem.transform.position)+Vector2.right*150;
            Call(input,"ProcessPointerMove",end);Call(input,"ProcessPointerUp",end);
            // ≡ 메뉴 수업 (사용자 10-13): 메뉴 → 딜 기여도 → 메뉴 → 선택지 보관함 → 메뉴 → 설정 위치
            var tutorialMenu=Object.FindFirstObjectByType<IngameHudMenu>();
            var tutorialChart=Object.FindFirstObjectByType<IngameDamageChart>();
            var tutorialInventory=Object.FindFirstObjectByType<SelectionInventoryUI>();
            var tutorialSettings=Object.FindFirstObjectByType<HSD.UI.Setting.UI_SettingPanel_InGame>(FindObjectsInactive.Include);
            async UniTask Instruction(string text) { await UniTask.WaitUntil(()=>Get<TMPro.TMP_Text>(dialogue,"_instruction").text==text,cancellationToken:token); await FocusReady(overlay); await Frames(); }
            async UniTask OpenMenu() { await Instruction(settings.MenuReopenInstruction); ClickButton(tutorialMenu.MenuButton); await UniTask.WaitUntil(()=>tutorialMenu.IsExpanded && tutorialMenu.IsSettled,cancellationToken:token); }
            await Stage(IngameTutorialStage.OptionsMenu);
            await Instruction(settings.MenuOpenInstruction);
            Check(Time.timeScale==0 && !input.AllowPointerClicks && tutorialMenu.gameObject.activeInHierarchy,"Menu lesson pauses the field before the final battle with the menu visible");
            ClickButton(tutorialMenu.MenuButton);
            await UniTask.WaitUntil(()=>tutorialMenu.IsExpanded && tutorialMenu.IsSettled,cancellationToken:token);
            await Instruction(settings.MenuDamageInstruction);
            ClickButton(tutorialMenu.DamageButton);
            await Instruction(settings.MenuDamageExplanation);
            Check(tutorialChart.IsOpen && tutorialChart.VisibleRowCount>0,"Menu lesson opens the actual damage chart with tutorial damage");
            await UniTask.WaitUntil(()=>!dialogue.IsTyping,cancellationToken:token);await FocusReady(overlay);
            await Snapshot("08a-menu-damage");
            await Acknowledge();
            await OpenMenu();
            await Instruction(settings.MenuSelectionInstruction);
            ClickButton(tutorialMenu.SelectionButton);
            await Instruction(settings.MenuSelectionExplanation);
            var expectedOwned=new[]{settings.LevelUpChoices[0]}.Concat(settings.GrantedChoices).ToArray();
            Check(tutorialInventory.IsOpen && tutorialInventory.ItemCount==expectedOwned.Length && expectedOwned.Select(c=>c.chooseId).SequenceEqual(new[]{9107,9102,9105}),"Selection inventory shows the picked choice plus the granted 9102 and 9105");
            await Snapshot("08b-menu-selection");
            await Acknowledge();
            await UniTask.WaitUntil(()=>!tutorialInventory.IsOpen,cancellationToken:token);
            await OpenMenu();
            await Instruction(settings.MenuSettingsExplanation);
            Check(!tutorialSettings.AllowOpening && settings.MenuSettingsExplanation.Contains("튜토리얼 중에는 잠시 이용할 수 없어요."),"Settings lesson explains the scene-wide settings lock");
            await UniTask.WaitUntil(()=>!dialogue.IsTyping,cancellationToken:token);await FocusReady(overlay);
            await Snapshot("08c-menu-settings");
            await Acknowledge();
            await UniTask.WaitUntil(()=>Get<TMPro.TMP_Text>(dialogue,"_instruction").text==settings.CombatPracticeInstruction,cancellationToken:token);
            Check(!tutorialMenu.IsExpanded && !tutorialChart.IsOpen && !tutorialInventory.IsOpen,"Menu lesson closes every window before the final battle");
            Check(!input.AllowPointerClicks && Time.timeScale==0,"Final combat explanation blocks gameplay input");
            await Acknowledge();
            await UniTask.WaitUntil(()=>boss.CurrentBoss!=null && Get<WaveManager>(director,"_wave").CurrentWave==2,cancellationToken:token);
            Check(boss.CurrentBoss.MaxHp==4500,"Wave 3 practice boss uses authored 4500 HP");
            var optionsMenu=Object.FindFirstObjectByType<IngameHudMenu>();
            var acquired=Object.FindFirstObjectByType<SelectionInventoryUI>();
            var time=Get<TimeScaleService>(director,"_time");
            var optionsFixture=new object();
            time.Pause(optionsFixture);
            try
            {
                ClickButton(Get<Button>(optionsMenu,"_menuButton"));
                await UniTask.Delay(400,DelayType.Realtime,cancellationToken:token);
                ClickButton(Get<Button>(optionsMenu,"_selectionButton"));
                await UniTask.Delay(800,DelayType.Realtime,cancellationToken:token);
                Check(acquired.IsOpen && acquired.ItemCount==1+settings.GrantedChoices.Length && acquired.Selected!=null && settings.GrantedChoices.Contains(acquired.Selected.Card),"Tutorial menu displays its actually acquired level-up choices");
                time.Release(optionsFixture);
                Check(time.Current==0 && time.IsRequesting(acquired),"Tutorial selection inventory owns its gameplay pause");
                await Snapshot("08-acquired-options");
                ClickButton(Get<Button>(acquired,"_closeButton"));
                await UniTask.Delay(250,DelayType.Realtime,cancellationToken:token);
                Check(!acquired.IsOpen && time.Current==1 && input.AllowPointerClicks,"Closing tutorial selection inventory resumes practice combat");
                ClickButton(optionsMenu.MenuButton);
                await UniTask.WaitUntil(()=>optionsMenu.IsExpanded && optionsMenu.IsSettled,cancellationToken:token);
                ClickButton(optionsMenu.SettingsButton);
                await tutorialSettings.OpenAsync();
                Check(!tutorialSettings.gameObject.activeInHierarchy && time.Current==1 && input.AllowPointerClicks,"Practice combat blocks menu and direct settings entry without interrupting the battle");
            }
            finally { time.Release(optionsFixture); }
            await UniTask.WaitUntil(()=>director.IsComplete,cancellationToken:token);
            Check(totem.RotationStep==1,"Hold then drag right rotates the totem");
            var effectAfter=totem.Data.GetEffectCells(totem,grid).ToArray();
            Check(!effectAfter.SequenceEqual(effectBefore) && effectAfter.All(c=>c.HasSpeedBuff) && effectBefore.Except(effectAfter).All(c=>!c.HasSpeedBuff),"Rotation moves the attack-speed buff and removes it from the old cells");
            var result=Object.FindFirstObjectByType<ResultScreenView>();
            Check(!input.AllowPointerClicks && result!=null && !Get<GameObject>(director,"_completionPanel").activeInHierarchy,"Wave 3 completion blocks gameplay behind the shared battle result instead of the old completion overlay");
            Check(game.CurrentState==GameManager.GameState.Win && game.ResultSnapshot!=null && game.ResultSnapshot.Round==3 && game.ResultSnapshot.BossKills==3,"Tutorial result commits its actual Wave 3 victory and three boss kills");
            Check(!Get<Button>(result,"_retryButton").gameObject.activeSelf && Get<Button>(result,"_homeButton").gameObject.activeInHierarchy,"Tutorial result leads to the lobby without offering an unintended replay");
            await UniTask.Delay(5500,DelayType.Realtime,cancellationToken:token);
            await Snapshot("10-wave3-battle-result");
            Check(!dialogue.gameObject.activeInHierarchy && !overlay.gameObject.activeInHierarchy,"Dialogue and highlight close together");
            Check(invUi.AllowClose,"Tutorial restores normal inventory closing");
            await CheckCompositionAsync(director,Check,token);
            log.AppendLine("COMPLETE — scripted handlers; Android touch and visual timing still require device review.");
        }
        catch(Exception e){log.AppendLine("FAIL "+e);Debug.LogException(e);}
        finally
        {
            if(hadProgress) PlayerPrefs.SetInt(completedKey,previousProgress); else PlayerPrefs.DeleteKey(completedKey);
            PlayerPrefs.Save();
            File.WriteAllText(report,log.ToString());Debug.Log(log.ToString());
            if (firstBossBalanceOnly) EditorApplication.isPlaying = false;
        }
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
            check(copy.Validate()==null,"Designer can insert reusable awareness into the tutorial recipe");
            copy.Lessons.RemoveAt(0);
            check(copy.Validate()!=null,"Sequence rejects missing gameplay prerequisites");
            time.Pause(otherPause);
            var awareness=Run(token);await UniTask.WaitUntil(()=>dialogue.gameObject.activeSelf,cancellationToken:token);
            dialogue.CompleteTyping();
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
    private static void Call(object obj,string method,int value)=>obj.GetType().GetMethod(method,Private).Invoke(obj,new object[]{value});
    private static void Drag(InputManager input,Vector2 from,Vector2 to)
    {
        Physics2D.SyncTransforms();
        Call(input,"ProcessPointerDown",Camera.main.WorldToScreenPoint(from));
        Call(input,"ProcessPointerMove",Camera.main.WorldToScreenPoint(to));
        Call(input,"ProcessPointerUp",Camera.main.WorldToScreenPoint(to));
    }
}
