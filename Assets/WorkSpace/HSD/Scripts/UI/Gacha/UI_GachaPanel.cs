using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UI_GachaPanel : UI_Base
{
    [Header("Background")]
    [SerializeField] private GameObject background;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button chanceButton;
    [SerializeField] private UI_GachaChancePanel chancePanel;
    [SerializeField] private UI_GachaButton gachaButton;
    [SerializeField] private UI_GachaButton gacha10Button;

    [Header("Animations")]
    [SerializeField] protected Vector2 startScale = new Vector2(.6f, .6f);
    [SerializeField] protected Vector2 endScale = Vector2.one;
    [SerializeField] protected float openDuration = 0.2f;
    [SerializeField] protected float closeDuration = 0.2f;
    [SerializeField] protected AnimationCurve openEase;
    [SerializeField] protected AnimationCurve closeEase;

    [Header("Production")]
    [SerializeField] private UI_GachaProduction production;

    private GachaSystem<ICharacterData> _gachaSystem;

    private void Awake()
    {
        _gachaSystem = Table.Gacha.CharacterGacha;
    }

    private void OnEnable()
    {
        closeButton.onClick.AddListener(Close);
        chanceButton.onClick.AddListener(ShowChancePopup);

        if (production != null) production.ResetProduction();
        RefreshButtons();
    }

    private void OnDisable()
    {
        closeButton.onClick.RemoveListener(Close);
        chanceButton.onClick.RemoveListener(ShowChancePopup);
    }

    private void ShowChancePopup()
    {
        if (chancePanel != null)
        {
            chancePanel.Open();
        }
    }

    public void Setup()
    {
        if (production != null) production.ResetProduction();
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        gachaButton.Refresh(_gachaSystem.Cost, () => StartGachaCycle(1).Forget());
        if (gacha10Button != null)
        {
            gacha10Button.Refresh(_gachaSystem.Cost * 10, () => StartGachaCycle(10).Forget());
        }
    }

    private async UniTaskVoid StartGachaCycle(int count)
    {
        // 1. 중복 클릭 방지 및 UI 잠금
        SetInteractable(false);

        try
        {
            // 2. 가챠 연출 실행 (별도 클래스에서 처리)
            if (production != null)
            {
                await production.PlayAsync();
            }

            // 3. 실제 결과 데이터 획득
            List<ICharacterData> results = new List<ICharacterData>();
            for (int i = 0; i < count; i++)
            {
                results.Add(_gachaSystem.Draw());
            }

            // 4. 결과창 띄우기 (결과창 클래스 구현 후 연결 필요)
            // await UI_GachaResultWindow.Show(results);
            Debug.Log($"[Gacha] {count}회 가챠 완료. 첫 번째 결과: {results[0].Name}");
            
            // 연출 리셋
            if (production != null) production.ResetProduction();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Gacha] 가챠 사이클 중 오류 발생: {e.Message}");
        }
        finally
        {
            // 5. UI 갱신 및 잠금 해제
            RefreshButtons();
            SetInteractable(true);
        }
    }

    private void SetInteractable(bool interactable)
    {
        closeButton.interactable = interactable;
        chanceButton.interactable = interactable;
        gachaButton.SetInteractable(interactable);
        if (gacha10Button != null) gacha10Button.SetInteractable(interactable);
    }

    protected override async UniTask OpenAnimationAsync()
    {
        if (background != null) background.SetActive(true);
        transform.localScale = startScale;
        await transform.DOScale(endScale, openDuration).SetEase(openEase).ToUniTask();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        await transform.DOScale(startScale, closeDuration).SetEase(closeEase).ToUniTask();
        if (background != null) background.SetActive(false);
    }
}
