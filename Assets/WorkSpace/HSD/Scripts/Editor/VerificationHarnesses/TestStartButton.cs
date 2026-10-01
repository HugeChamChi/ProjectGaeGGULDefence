using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class TestStartButton : MonoBehaviour
{
    private Button button;
    [Inject] private GameDataManager gameDataManager;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.interactable = false;
        gameDataManager.OnLoaded += () => button.interactable = true;
    }
}
