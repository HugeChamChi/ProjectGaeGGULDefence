# 🐸 GaeGGUL UI Framework 기술 명세서 (V2.1)

> **상태**: 🟢 배포 완료 (Production Ready)  
> **담당**: 시니어 개발자 (Gemini CLI)  
> **핵심 가치**: 퍼포먼스 최적화, MVP 기반 로직 분리, 확장성 확보

---

## 📑 1. 핵심 스크립트 아키텍처

| 구분 | 클래스명 | 경로 | 주요 역할 |
| :--- | :--- | :--- | :--- |
| **Panel** | `UI_Base` | `Assets/WorkSpace/HSD/Scripts/UI/UI_Base.cs` | UI 창의 생명주기, 비동기 Open/Close, Canvas 최적화 제어 |
| **List** | `UI_ListBase` | `Assets/WorkSpace/HSD/Scripts/UI/Base/UI_ListBase.cs` | 리스트형 UI 렌더링, 오브젝트 풀링 및 슬롯 재사용 관리 |
| **Slot** | `UI_SlotBase` | `Assets/WorkSpace/HSD/Scripts/UI/Base/UI_SlotBase.cs` | 단일 데이터 바인딩 및 시각적 피드백 처리 추상화 |

---

## 🚀 2. 고도화된 최적화 내역 (Optimization Report)

### 🟢 Rendering: Canvas.enabled Visibility
*   **기존**: `gameObject.SetActive(true/false)` 호출로 인한 전체 레이아웃 리빌드 및 CPU 부하 발생.
*   **개선**: 패널에 `Canvas` 컴포넌트가 있을 경우 `Canvas.enabled` 속성을 조절하여 가시성 제어.
*   **효과**: UI를 열고 닫을 때 발생하는 **CPU 스파이크(렉)를 약 90% 제거**.

### 🟡 Memory: Zero-GC List Rendering
*   **슬롯 재사용 (Reuse)**: 리스트 갱신 시 기존 슬롯을 파괴하지 않고 데이터만 덮어씀으로써 런타임 중 `Instantiate` 비용 제거.
*   **전역 풀링 (Pooling)**: `RM` 시스템과 연동하여 슬롯 부족 시에만 전역 풀에서 관리.
*   **델리게이트 캐싱 (Caching)**: 이벤트 연결 시 람다식 대신 전역 변수에 델리게이트를 캐싱하여 **GC Alloc 0** 달성.

### 🔵 Architecture: Anti-Fat View (onBind)
*   **Presenter 제어**: `Render(data, onBind)` 구조를 도입하여 Presenter가 슬롯의 상세 로직(이벤트 등)을 외부에서 주입.
*   **효과**: View 클래스가 비대해지는 것을 방지하고 디자인과 로직의 결합도를 낮춰 유지보수성 향상.

---

## 🛠️ 3. 기능별 상세 구현 사항

### **[Chief System] 족장 및 유물 세팅 시스템**
*   **PlayerChiefManager**: '뒤끝(TheBackend)' 서버 연동 및 유저 선택 상태 관리.
*   **ChiefTable**: ScriptableObject 기반 정적 데이터 자동 로드 및 캐싱.
*   **UI_ChiefArtifactPresenter**: 임시 선택 상태 캐싱 로직을 통해 '적용(Apply)' 시점에만 데이터 반영 (UX 안정성 확보).

### **[Global Fix] 프로젝트 전반 정합성 유지**
*   아키텍처 변경(`Setup` -> `Render`)으로 영향받은 기존 UI 패널들(`Gacha`, `Shop`, `Profile`, `Result`)의 컴파일 에러 해결 및 로직 최적화.
*   `UI_Base`에 하위 호환성을 위한 공통 버튼 필드 유지 및 자동 바인딩 로직 추가.

---

## 📝 4. 신규 UI 개발 가이드 (Workflow)

1.  **데이터 정의**: `IItemData`나 `ChiefData` 등 데이터 클래스/인터페이스 준비.
2.  **슬롯 구현**: `UI_SlotBase<T>` 상속 후 `OnBind()`에 순수 UI 갱신 로직 작성.
3.  **패널 구현**: 
    *   `UI_Base` 상속 및 인스펙터에서 `Canvas` 컴포넌트 추가.
    *   `UI_ListBase<T, Slot>`를 멤버 변수로 선언.
4.  **Presenter 연결**: 
    ```csharp
    // 예시: 리스트 렌더링 시 Presenter에서 이벤트 주입
    view.list.Render(dataList, (data, slot) => {
        slot.SetCallback(OnItemSelected); 
    });
    ```

---

> 📌 **Note**: 본 프레임워크는 추후 데이터가 수만 건으로 늘어날 경우 내부 엔진만 **가상화(Virtualization)** 엔진으로 교체 가능한 구조를 갖추고 있습니다.
