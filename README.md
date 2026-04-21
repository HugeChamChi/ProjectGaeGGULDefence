# 🐸 개구리 디펜스 (GaeGGUL Defence)

---

## 📝 커밋 규칙

| 유형 | 내용 |
|---|---|
| `Feat` | 새로운 기능 추가 |
| `Fix` | 버그 수정 |
| `Build` | 빌드 관련 수정 |
| `Test` | 테스트 씬 또는 코드 추가 |
| `Refactor` | 코드 리팩토링 |
| `Docs` | 주석이나 문서 수정 (README 등) |
| `Release` | 버전 릴리즈 |
| `Create` | 파일 추가 및 프로젝트 생성 |
| `Chore` | 간단한 수정 |

```
예시: [Feat] 개구리 궁수 유닛 공격 로직 추가
```

### Branch 네이밍

| 브랜치 | 용도 | 예시 |
|---|---|---|
| `GGD` | 기획 Branch | `GGD_SY`, `GGD_기획내용` |
| `Develop` | 개발 Branch | `USW_Frog`, `KMS_Grid` |

- **기획 Branch** : `GGD_` 뒤에 본인 이니셜 또는 작업 내용을 붙인다.
- **개발 Branch** : `이니셜_작업내용` 형식으로 작성한다.

---

## 📐 기타 규칙

- **작업 위치** : `Assets/WorkSpace/` 내 자신의 이니셜 폴더에서 작업한다.
- **씬 분리** : 팀원 간 서로 다른 씬에서 작업한다.
- **Unity LifeCycle** : `Awake`, `Start`, `Update` 등은 별도로 모아서 정리한다.
- **단일 책임 준수**
- **ScriptableObject 활용** : 게임 밸런스 수치, 설정값, 리소스 정보는 ScriptableObject로 관리한다.
- **인터페이스로 역할 정의** : 역할은 인터페이스로 정의하고 구현은 클래스에서 한다.
- **데이터와 로직 분리** : 데이터 보관과 처리 로직을 분리하는 것을 지향한다.

---

## 🎬 테스트 씬 네이밍 규칙

테스트용 씬 파일은 이름 앞에 `Test_` 를 붙인다.

```
예시: Test_FrogUnitSpawn, Test_WaveSystem
```
