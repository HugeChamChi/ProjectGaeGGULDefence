using System.Collections.Generic;
using UnityEngine;
using GaeGGUL.Tutorial;
using Cysharp.Threading.Tasks;

public class Test_TutorialSystem : MonoBehaviour
{
    [VContainer.Inject] private TutorialManager _tutorialManager;
    [Header("Sequence Data (Option)")]
    [SerializeField] private TutorialSequence _testSequence;

    [Header("Testing Actors")]
    [SerializeField] private TutorialActor _mockGachaActor;
    [SerializeField] private GameObject _mockGachaButton;
    [SerializeField] private GameObject _mockCloseButton;


    [Button]
    public void SetupAndRunGachaTest()
    {
        if (_tutorialManager != null) _tutorialManager.PlaySequenceAsync(_testSequence).Forget();
    }

    [Button]
    public void ClearRegistry()
    {
        TutorialRegistry.Clear();
        Debug.Log("[Test] 레지스트리 초기화 완료");
    }

    [Button]
    public void TestDimOn() { if (_tutorialManager != null) _tutorialManager.SetDimAsync(true).Forget(); }

    [Button]
    public void TestDimOff() { if (_tutorialManager != null) _tutorialManager.SetDimAsync(false).Forget(); }
}
