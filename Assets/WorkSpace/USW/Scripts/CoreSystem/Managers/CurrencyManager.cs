// ════════════════════════════════════════════════════════
// CurrencyManager — InGameSingleton 교체
// ════════════════════════════════════════════════════════
using UnityEngine;
using System;

public class CurrencyManager : MonoBehaviour
{ 
    public void Init()
    {
        
    }

    public event Action<float> OnCurrencyChanged;
    public float Currency { get; private set; }

    public void AddCurrency(float amount)
    {
        Currency += amount;
        OnCurrencyChanged?.Invoke(Currency);
    }

    public bool Spend(float amount)
    {
        if (Currency < amount) return false;
        Currency -= amount;
        OnCurrencyChanged?.Invoke(Currency);
        return true;
    }
}
