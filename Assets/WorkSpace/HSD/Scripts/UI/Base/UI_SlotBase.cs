using UnityEngine;
using System;

/// <summary>
/// 모든 UI 슬롯의 최상위 부모 클래스
/// </summary>
/// <typeparam name="T">데이터 타입</typeparam>
public abstract class UI_SlotBase<T> : MonoBehaviour
{
    protected T _data;
    public T Data => _data;

    // 데이터를 세팅하고 UI를 갱신합니다.
    public virtual void SetData(T data)
    {
        _data = data;
        OnBind();
    }

    // 실제 UI 요소(텍스트, 이미지 등)를 데이터와 연결하는 추상 함수
    protected abstract void OnBind();

    // 선택 상태 등 공통 시각적 피드백 처리
    public virtual void SetSelected(bool isSelected) { }
}
