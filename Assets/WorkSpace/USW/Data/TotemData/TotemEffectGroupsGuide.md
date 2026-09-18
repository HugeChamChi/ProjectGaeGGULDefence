# 효과별 토템 범위와 색상 설정

`SampleDualEffectTotemData.asset`을 복제해서 사용하세요. 왼쪽 1칸은 공격력 +10%(주황), 오른쪽 1칸은 공격속도 +20%(파랑)인 샘플입니다. 선택지 풀에는 자동 추가하지 않았으므로, 게임에서 선택하려면 IngameScene의 TotemSelectPanel 토템 풀에 넣어주세요. 복제본의 ID도 고유하게 변경하세요.

## TotemData 설정

- `UseSheetData`: 끄기. SO 값을 사용합니다.
- 프리팹: `GenericBuffTotem`을 사용하는 프리팹. 샘플은 기존 `SampleAttackTotem` 주소를 공유합니다.
- `EffectGroups`: 효과마다 항목을 하나씩 추가합니다.
  - `Label`: 범례에 보여줄 짧은 이름(공격력, 공격속도 등).
  - `Description`: 해당 색상으로 표시할 설명.
  - `Color`: 설명, 범례, 작은 범위도, 실제 그리드 빗금의 공통 색상.
  - `Functions`: 이 범위에 실제 적용할 효과와 수치.
  - `Ranges`: 이 효과가 적용될 상대 범위.

EffectGroups가 있으면 기존 공통 Functions/Effect Ranges 대신 그룹별 설정으로 적용됩니다. 그룹끼리 범위가 겹치지 않게 작성하세요. 단일 효과도 그룹 하나로 작성하면 개별 색상을 지정할 수 있습니다. 그룹이 없는 기존 토템은 기존 효과/범위를 계속 사용합니다. 설명은 수치에서 자동 생성되지 않으므로 효과 수치를 수정할 때 Description도 함께 수정하세요.

## 빗금 조절

`Assets/WorkSpace/USW/Materials/TotemEffectRange.mat`에서 조절합니다.

| 속성 | 용도 |
|---|---|
| Fill Color | 빗금 뒤 바탕색/투명도 |
| Stripe Color | 빗금 기본색/투명도 |
| Stripe Spacing | 선 간격(월드 단위) |
| Stripe Width | 선 두께 비율 |
| Stripe Angle | 기울기 |
| Scroll Speed | 흐르는 속도, 음수는 반대 방향 |
| Use Sprite Alpha | 현재 셀 텍스처에서는 끄기 유지 |

그룹을 사용하면 색상의 RGB는 그룹 Color를 따르고, 투명도는 그룹 알파와 머티리얼 알파가 함께 반영됩니다. 기본 셀 텍스처 `GridCell_Overlay.png`는 완전히 투명하므로 Use Sprite Alpha를 켜면 빗금도 사라집니다. 셰이더는 셀 메시 안에서 직접 빗금을 그립니다. 일시정지 중에도 흐름은 유지됩니다.

배치된 토템 터치, 회전 미리보기, 인벤토리에서 빈 셀로 드래그할 때 범위를 표시합니다. 드래그 미리보기는 실제 버프를 적용하거나 아이템을 소비하지 않습니다. 공격 불가능 범위는 표시하지 않습니다.

## 검증

`TotemRangePreviewChecks.Run()`을 새로운 Play Mode 세션에서 실행하면 실제 탭, 그룹별 버프/색상, 인벤토리 호버/취소, 일시정지 중 빗금 시간 등 20개 항목을 검사합니다. 결과는 `Temp/totem-range-checks.txt`, 화면은 `Temp/totem-range-tap.png` 및 `Temp/totem-range-dual.png`에 기록됩니다. Android 기기의 터치 감각 및 렌더링 성능은 별도 실기기 확인이 필요합니다.
