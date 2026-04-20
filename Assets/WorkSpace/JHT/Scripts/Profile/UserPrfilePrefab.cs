using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class UserPrfilePrefab : MonoBehaviour
{
    protected bool isOpen;
    public bool IsOpen { get { return isOpen; } set { isOpen = value;  OnOpen?.Invoke(isOpen); } }
    public event Action<bool> OnOpen;

    public ProfileDataSO dataSO;
    protected abstract void ClickIcon();
}
