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
        const string completedKey = "IngameTutorial.Completed.v1";
        bool hadProgress = PlayerPrefs.HasKey(completedKey);
        int previousProgress = PlayerPrefs.GetInt(completedKey, 0);
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
            await FocusReady(overlay);
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
            float observedAt=Time.unscaledTime;
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
            int previousLevel=item.CurrentLevel;
            overlay.OnPointerClick(pointer);await Frames();
            Check(director.CurrentStage==IngameTutorialStage.UpgradeSlots,"Background tap cannot skip actual upgrade");
            var upgradeBuy=(Button)new SerializedObject(item).FindProperty("btn_Upgrade").objectReferenceValue;
            ClickButton(upgradeBuy);
            await Frames();
            Check(Get<HSD.UI.Upgrade.UI_UpgradePanel>(director,"_upgradePanel").StatFeedbackArea != null,"Upgrade opens the unit stat feedback panel");
            var statPanel = Object.FindFirstObjectByType<GaeGGUL.UI.Unit.UI_UnitInfoPanel>();
            Check(statPanel != null && Get<bool>(statPanel,"_upgradeFeedback") && statPanel.GetComponentsInChildren<TMPro.TMP_Text>().Count(t=>t.text.Contains("→")) == 2,
                "Upgrade feedback displays both before/after stat values");
            dialogue.CompleteTyping();
            await UniTask.Delay(450,DelayType.Realtime,cancellationToken:token);
            await Snapshot("07-upgrade-stat-feedback");
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
            Check(cards.All(c=>!c.AllowSelection) && !overlay.gameObject.activeSelf,"Opening the real tooltip hides the tutorial mask and keeps selection locked");
            upgrade.OnPointerClick(pointer);
            Check(!upgrade.IsInteractable() && !Get<HSD.UI.Upgrade.UI_UpgradePanel>(director,"_upgradePanel").IsOpen,
                "Choice term explanation blocks opening the upgrade panel");
            await Snapshot("06-choice-term-popup");
            var termPopup=Object.FindFirstObjectByType<DebuffInfoPopup>();
            ClickButton(termPopup.transform.Find("SafeArea/Panel/Close").GetComponent<Button>());
            Check(!card.AllowSelection,"Tooltip close animation must finish before selection unlocks");
            await UniTask.WaitUntil(()=>card.AllowSelection && !level.DescriptionBlocksInput,cancellationToken:token);
            Check(game.CurrentState==GameManager.GameState.LevelUp,"Closing the tooltip returns to the choices without selecting one");
            Check(card.GetData().chooseName == "넹?" && cards.Count(c=>c.AllowSelection) == 1,
                "Tutorial selection permits only the Neng-question card");
            var otherChoice = cards.First(c=>c != card);
            otherChoice.OnPointerDown(pointer);otherChoice.OnPointerUp(pointer);otherChoice.OnPointerClick(pointer);
            await Frames();
            Check(game.CurrentState==GameManager.GameState.LevelUp,"Other choice clicks cannot bypass the required tutorial reward");
            await FocusReady(overlay);
            card.OnPointerDown(pointer);card.OnPointerUp(pointer);card.OnPointerClick(pointer);
            await Stage(IngameTutorialStage.ChiefSkill);
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
            ClickButton(chief);
            await Frames();
            Check(!patterns.IsCounterablePattern(boss.CurrentBoss),"Actual chief button cancels the earthquake before impact");
            Check(grid.AllCells().All(c=>!c.Model.IsBossTelegraphPreviewed),"Successful counter clears the field warning");
            await Stage(IngameTutorialStage.TotemChoice);
            Check(boss.CurrentBoss == null || (!boss.CurrentBoss.Invincible && !boss.CurrentBoss.PreventDeath),
                "Boss can be defeated after reward learning");
            var reward=Get<TotemRewardUI>(director,"_rewardUI");
            await UniTask.WaitUntil(()=>reward.IsReadyForSelection,cancellationToken:token);await Frames();
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
            Call(reward,"OnDetailSwipe",1);await Frames();
            Check(Get<int>(reward,"_detailIndex")==0,"Detail swipe cannot change the required tutorial totem");
            await FocusReady(overlay);
            dialogue.CompleteTyping();await Snapshot("09-forced-totem-confirm");
            var confirm=reward.GetComponentsInChildren<Button>().First(b=>b.name.Contains("Confirm"));ClickButton(confirm);
            await Stage(IngameTutorialStage.OpenInventory);
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
            await Stage(IngameTutorialStage.RotateTotem);
            await FocusReady(overlay);await Frames();
            var totem=Get<TotemBase>(director,"_placedTotem");
            var effectBefore=totem.Data.GetEffectCells(totem,grid).ToArray();
            Check(totem.Data.GetSimpleAmount(StatKind.Speed)==0.1f && effectBefore.Length>0 && effectBefore.All(c=>c.HasSpeedBuff),"Placed tutorial totem applies attack speed to its actual cells");
            Check(totem.Data.isRotatable && totem.CurrentCell==expectedCell,"Installed totem proceeds directly to rotation");
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
            await UniTask.WaitUntil(()=>director.IsComplete,cancellationToken:token);
            Check(totem.RotationStep==1,"Hold then drag right rotates the totem");
            var effectAfter=totem.Data.GetEffectCells(totem,grid).ToArray();
            Check(!effectAfter.SequenceEqual(effectBefore) && effectAfter.All(c=>c.HasSpeedBuff) && effectBefore.Except(effectAfter).All(c=>!c.HasSpeedBuff),"Rotation moves the attack-speed buff and removes it from the old cells");
            Check(input.CanBeginInteraction==null && input.CanEndInteraction==null && input.AllowPointerClicks,"Completion restores normal world input");
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
            File.WriteAllText(Report,log.ToString());Debug.Log(log.ToString());
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
