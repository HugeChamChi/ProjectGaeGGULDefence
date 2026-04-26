# UniTask 마이그레이션 기술 문서

**작업 브랜치**: `USW_LobbySceneRecover`  
**작업 범위**: `Assets/WorkSpace/USW/Scripts/` 전체  
**대상 파일**: 8개

---

## 배경

프로젝트는 UniTask 2.5.10을 도입했으나, 기존 비동기 처리가 모두 Unity Coroutine(`IEnumerator`)으로 작성되어 있었다.  
Coroutine은 CancellationToken을 지원하지 않아 오브젝트 파괴 후 루프가 잔존하거나, 실패 시 대기 상태에서 탈출할 수 없는 구조적 문제가 있었다.  
이번 작업은 Coroutine을 UniTask 기반 async/await로 전환하여 수명 관리와 예외 추적을 명확히 했다.

---

## 공통 적용 원칙

### 1. UniTask + .Forget(Debug.LogException)

```csharp
// Before
_coroutine = StartCoroutine(SomeRoutine());

// After
_cts = new CancellationTokenSource();
SomeAsync(_cts.Token).Forget(Debug.LogException);
```

`UniTaskVoid`는 호출 측에서 예외 흐름 제어가 어렵다.  
`UniTask` + `.Forget(Debug.LogException)`을 사용해 취소 외 예외를 Unity 콘솔에 명시적으로 출력한다.

### 2. OperationCanceledException 정상 처리

```csharp
try
{
    while (true)
    {
        token.ThrowIfCancellationRequested();
        // ...
        await UniTask.Delay(..., cancellationToken: token);
        token.ThrowIfCancellationRequested();
        // 작업 실행
    }
}
catch (OperationCanceledException) { }  // 정상 취소 — 무시
```

- 루프 진입 시 `token.ThrowIfCancellationRequested()` 로 즉시 취소 감지
- 중요 작업 직전에도 체크해 취소 타이밍 경계 방어

### 3. CancellationTokenSource 전략

파일 성격에 따라 세 가지 전략을 구분해 적용했다.

| 전략 | 적용 기준 | 적용 파일 |
|------|-----------|-----------|
| `new CancellationTokenSource()` | 명시적 API(UnregisterBoss 등)로 수명 관리되는 싱글톤 | `BossPatternController`, `TotemTimedBuff` |
| `CreateLinkedTokenSource(GetCancellationTokenOnDestroy())` | 수동 취소 + Destroy 취소가 공존하는 프리팹/UI | `PlayerDataController`, `LevelUpCardUI`, `Projectile`, `LoginSceneManager` |
| `GetCancellationTokenOnDestroy()` 직접 사용 | 필드 관리 불필요한 일회성 타이머 | `GridCell` |

### 4. 최솟값 방어

ScriptableObject 또는 SerializeField 값이 0·음수일 때 루프 폭발 방지.

```csharp
rowSpeedMult = Mathf.Max(rowSpeedMult, 0.01f);  // 나눗셈 분모
interval     = Mathf.Max(interval, 0.05f);       // 최소 50ms
```

---

## 파일별 변경 내용

### 1. `Unit/UnitBase.cs`

**변경 전**
- `Coroutine _gaugeCoroutine` + `WaitForSeconds _cachedWait` + `float _cachedInterval`
- `IEnumerator GaugeLoop()` — while(true), CancellationToken 없음

**변경 후**
- `CancellationTokenSource _gaugeCts`
- `StopGaugeLoop()` 헬퍼, `OnDestroy()` 추가
- `async UniTask GaugeLoopAsync(CancellationToken token)`
- `rowSpeedMult` / `interval` 최솟값 방어 (`0.01f` / `0.05f`)
- `.Forget(Debug.LogException)`

**주요 포인트**  
게임 핵심 루프. 셀 봉인 시 `await UniTask.Yield(token)` 으로 1프레임 대기하며 취소 가능.  
`WaitForSeconds` 캐싱 로직(`_cachedWait`, `_cachedInterval`) 제거 — UniTask.Delay는 GC 부담 없음.

---

### 2. `OutGame/PlayerDataController.cs`

**변경 전**
- `Coroutine _staminaTimer`
- `IEnumerator StaminaTimerCoroutine()` — while(true), CancellationToken 없음
- `IEnumerator RecoverStaminaCoroutine()` — 동기 작업 후 `yield return null`만 있는 불필요한 코루틴
- `UpdateStaminaTimer()` — `_staminaTimer = null` 후 재시작 (기존 루프 미정지 버그)

**변경 후**
- `CancellationTokenSource _staminaLoopCts` (이름으로 의도 명시)
- `CreateLinkedTokenSource(GetCancellationTokenOnDestroy())` — OnDisable 수동 취소 + Destroy 자동 취소
- `async UniTask StaminaTimerAsync(CancellationToken token)`
- `void RecoverStamina()` — 동기 메서드로 단순화
- `UpdateStaminaTimer()` — 스태미나 풀이면 `StopStaminaTimer()` 호출
- `OnDestroy()` 추가

**주요 포인트**  
`_staminaTimer = null` 버그 수정 — 기존 루프를 끊지 않고 null만 대입해 중복 루프 가능성이 있었다.  
`RecoverStaminaCoroutine`은 마지막 `yield return null`이 유일한 비동기 코드였고 아무도 await하지 않아 일반 메서드로 전환.

---

### 3. `Boss,Enemy/BossLogic/BossPatternController.cs`

**변경 전**
- `BossPatternEntry.timerCoroutines: List<Coroutine>` — 보스별 코루틴 목록
- `IEnumerator TimerPatternRoutine()` — 보스당 패턴 수만큼 독립 코루틴

**변경 후**
- `BossPatternEntry.timerCts: CancellationTokenSource` — 보스별 단일 CTS
- `async UniTask TimerPatternAsync(BossBase, BossPatternData, CancellationToken)`
- `UnregisterBoss()` / `UnregisterAll()` — `timerCts.Cancel()` + `Dispose()`
- `Mathf.Max(pattern.interval, 0.1f)` 방어

**주요 포인트**  
취소는 항상 "보스의 모든 패턴 일괄 취소"로만 일어남 → CTS 하나로 통합.  
`while (_entries.ContainsKey(boss) && !boss.IsDead)` 조건 유지 — 보스 사망 시 `UnregisterBoss`가 즉시 안 호출되는 엣지케이스 방어.  
`plain new CancellationTokenSource()` 선택 — 싱글톤이며 `UnregisterAll()`이 명시적 정리 경로이므로 DestroyToken 불필요.

---

### 4. `LevelUp/LevelUpCardUI.cs`

**변경 전**
- `Coroutine _animCoroutine`
- `Select()`에서 직접 `StartCoroutine` + `StopCoroutine`

**변경 후**
- `CancellationTokenSource _animCts`
- `StartAnim()` / `StopAnim()` 헬퍼 분리
- `CreateLinkedTokenSource(GetCancellationTokenOnDestroy())` — Deselect() 수동 취소 + Destroy 자동 취소
- `async UniTask PlayAnimationAsync(CancellationToken token)`

**주요 포인트**  
`StartAnim()` 헬퍼 분리로 `Select()`가 `if (hasAnim) StartAnim()` 한 줄로 단순화.  
`interval = 1f / Mathf.Max(_data.frameRate, 1f)` — 기존 방어 유지, 추가 guard 불필요.  
`frames`와 `interval`을 시작 시점에 로컬 변수로 캡처 — `_data` 중간 교체 시에도 일관된 애니메이션 보장.

---

### 5. `Totem/TotemTimedBuff.cs`

**변경 전**
- `Coroutine _cycleCoroutine`
- `IEnumerator CycleRoutine()` — while(true), CancellationToken 없음
- `RemoveBuff()`에서만 정리

**변경 후**
- `CancellationTokenSource _cycleCts`
- `async UniTask CycleAsync(CancellationToken token)`
- `Mathf.Max(_cycleDuration, 0.1f)` / `Mathf.Max(_burstDuration, 0.1f)` 방어
- catch 블록에서 `if (_burstActive) EndBurst()` — 버스트 중 취소 시 안전 해제

**주요 포인트**  
`plain new CancellationTokenSource()` 선택 — `TotemBase.OnDestroy()`가 `OnRemoved()` → `RemoveBuff()`를 호출하므로 Destroy 경로가 이미 커버됨.  
`EndBurst()` 중복 호출은 내부 `if (!_burstActive) return` 가드로 무해.

---

### 6. `Combat/Projectile.cs`

**변경 전**
- `Coroutine _moveCoroutine`
- `IEnumerator Move()` — `yield return null` 퍼프레임 Lerp
- `OnDisable()`에서 StopCoroutine, OnDestroy 없음

**변경 후**
- `CancellationTokenSource _moveCts`
- `async UniTask MoveAsync(Vector3 target, CancellationToken token)`
- `yield return null` → `await UniTask.Yield(token)` (퍼프레임, 취소 가능)
- `CreateLinkedTokenSource(GetCancellationTokenOnDestroy())` — 풀 반환(OnDisable) 수동 취소 + Destroy 자동 취소
- `OnDestroy()` 추가

**주요 포인트**  
루프 종료 후 `token.ThrowIfCancellationRequested()` 미적용 — 자연 완료 시 반드시 최종 위치 보정 + `_onComplete` 콜백이 실행돼야 하기 때문.  
`_onComplete?.Invoke(this)` → `SetActive(false)` → `OnDisable()` → `StopMove()` 순으로 CTS가 자동 정리됨.

---

### 7. `Grid/GridCell.cs`

**변경 전**
- `StartCoroutine(DebuffTimerRoutine(...))` — 코루틴 참조 저장 없음, 취소 수단 없음
- `IEnumerator DebuffTimerRoutine()` — `WaitForSeconds` + `onExpire`

**변경 후**
- CTS 필드 없음 — `GetCancellationTokenOnDestroy()` 직접 전달
- `async UniTask DebuffTimerAsync(float, Action, CancellationToken)`

**주요 포인트**  
가장 단순한 케이스. 일회성 타이머이며 여러 개가 동시에 독립 실행될 수 있는 구조.  
모든 타이머가 동일한 DestroyToken을 공유해 셀 파괴 시 일괄 취소.  
새 CTS 필드 관리 불필요.

---

### 8. `OutGame/LoginSceneManager.cs`

**변경 전**
- `StartCoroutine(LoadingBarRoutine())` — 참조 저장 없음
- `IEnumerator LoadingBarRoutine()` — `WaitUntil(() => _loginDone)` 포함
- **버그**: `OnLoginFailed()` 호출 시 `_loginDone = false`로 리셋되나, 실행 중인 코루틴의 `WaitUntil`은 탈출 불가 → 재시도 시 중복 코루틴 실행

**변경 후**
- `CancellationTokenSource _loadingBarCts`
- `StartLogin()` — 이전 CTS 취소 후 상태 초기화(`_loginDone`, `fillAmount`) + linked CTS 생성
- `OnLoginFailed()` — CTS 먼저 취소 → `WaitUntil` 대기 즉시 탈출
- `async UniTask LoadingBarAsync(CancellationToken token)` — 4단계 구조 유지
- `WaitUntil` → `await UniTask.WaitUntil(() => _loginDone, cancellationToken: token)`

**주요 포인트**  
단순 리팩토링이 아닌 **중복 실행 버그 수정**. 로그인 실패 후 재시도 시 이전 로딩 루프가 살아있던 문제 해결.

---

## 변경 후 전체 요약

| 파일 | 제거된 것 | 추가된 것 | CTS 전략 |
|------|-----------|-----------|----------|
| `UnitBase.cs` | `Coroutine`, `WaitForSeconds` 캐시 | `_gaugeCts`, `StopGaugeLoop()`, `OnDestroy()` | plain new |
| `PlayerDataController.cs` | `Coroutine`, `IEnumerator RecoverStamina` | `_staminaLoopCts`, `OnDestroy()` | LinkedToken |
| `BossPatternController.cs` | `List<Coroutine>` | `timerCts` (단일) | plain new |
| `LevelUpCardUI.cs` | `Coroutine` | `_animCts`, `StartAnim()` 헬퍼 | LinkedToken |
| `TotemTimedBuff.cs` | `Coroutine` | `_cycleCts` | plain new |
| `Projectile.cs` | `Coroutine` | `_moveCts`, `StopMove()`, `OnDestroy()` | LinkedToken |
| `GridCell.cs` | `IEnumerator` | DestroyToken 직접 전달 | DestroyToken 직접 |
| `LoginSceneManager.cs` | `IEnumerator`, 중복 버그 | `_loadingBarCts` | LinkedToken |
