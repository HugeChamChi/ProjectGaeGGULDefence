// ════════════════════════════════════════════════════════
// CurrencyManager — InGameSingleton 교체
// ════════════════════════════════════════════════════════
using UnityEngine;
using System;

public class CurrencyManager : MonoBehaviour
{ 
    [VContainer.Inject] private GameManager _gameManager;
    public void Init()
    {
        
    }

    public event Action<float> OnCurrencyChanged;
    public float Currency { get; private set; }

    public void AddCurrency(float amount)
    {
        if (_gameManager?.IsFinished == true) return;
        Currency += amount;
        OnCurrencyChanged?.Invoke(Currency);
    }

    public bool Spend(float amount)
    {
        if (_gameManager?.IsFinished == true) return false;
        if (Currency < amount) return false;
        Currency -= amount;
        OnCurrencyChanged?.Invoke(Currency);
        return true;
    }
}
