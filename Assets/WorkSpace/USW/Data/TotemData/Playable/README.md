# 토템 10종 사용/수정 가이드

각 번호의 `TD1001.prefab`은 외형·동작, `TD1001Data.asset`은 이름·효과·범위·수치를 담당합니다. 1001~1010 모두 `Use Sheet Data`를 껐고, KHJ_Artwork/Totem_Sprite의 같은 번호 그림을 연결했습니다. IngameScene의 TotemSelectPanel 토템 풀에는 10종이 등록되어 있습니다. 기존 샘플 토템은 에셋을 보존하고 선택지 풀에서만 제외했습니다.

## 바로 테스트하기

IngameScene을 실행하고 보스를 처치하면 토템 보상 후보가 나옵니다. 선택한 토템은 최대 5칸 인벤토리로 들어갑니다. 토템 버튼을 누르고 아이콘을 빈 셀로 드래그하세요. 임시 등급 상승/아머브레이크는 기본적으로 바로 위 1칸에 적용됩니다. 짧은 터치 후 드래그로 방향을 바꿀 수 있습니다.

## 어느 파일을 바꾸면 되나요?

| 번호 / SO | 동작 | 수정할 항목 |
|---|---|---|
| TD1001Data | 보조 투사체 | Bonus Projectile 확률 0.33, 계수 0.5; effectRanges |
| TD1002Data | 공격력 +5%, 처치별 범위 확장 | functions의 amount; Growth Stages의 처치 수/Offsets |
| TD1003Data | 5초마다 화염구 피해 100 + 화상 | Debuff Fire Interval / Impact Damage / Projectile; 화상 정의는 Resources/Debuffs/Burn |
| TD1004Data | 40초마다 조커 생성 | Wildcard Spawn 주기와 Unit |
| TD1005Data | 위 1칸 임시 등급 상승 | effectRanges; 스킬 SO까지 바꾸려면 Tier Upgrades |
| TD1006Data | 위 1칸 공격으로 아머 누적 | effectRanges; 아머 정의는 Resources/Debuffs/ArmorBreak |
| TD1007Data | 0.2초 지연 그림자 | Shadow Attack 지연/색상; effectRanges |
| TD1008Data | 안쪽 공격력 +10%, 바깥 공격속도 +10% | Effect Groups 각각의 Color / Description / Functions / Ranges |
| TD1009Data | 인접 공격력 +50%, 공격속도 -20% | functions 두 항목; effectRanges |
| TD1010Data | 10초마다 식량 30 | functions의 FoodGeneratorFunction interval / amount |

퍼센트 수치는 0.1=10%입니다. 설명은 수치 변경 시 직접 함께 수정해주세요. 그룹을 쓰는 TD1008은 각 Description을 수정합니다.

## 테스트용으로 정한 값

- 등급은 **10종 모두 Normal**입니다. 미확정 밸런스로, 각 SO의 Tier에서 바꿀 수 있습니다.
- TD1001/TD1007 범위는 인접 8칸으로 임시 설정했습니다.
- TD1008의 +10%/+10%, TD1009의 +50%/-20%, TD1010의 10초/30은 기존 제작 예시값입니다.
- 방향별 별도 그림이 없어 네 방향 모두 같은 원본 그림을 사용합니다. 방향 전환은 효과 범위에 적용됩니다. 방향 그림이 생기면 rotationSpriteAddresses 4개를 교체하세요.
- `TotemWildcardUnit`은 전용 조커 그림 대신 TD1004 그림을 임시 사용합니다. 노말 전용 합성 재료이며 랜덤 유닛 풀에는 넣지 않았습니다.
- `TotemProjectileData`는 기본 직선 투사체 구성을 사용합니다. 화염구/보조 발사의 전용 이펙트는 여기서 별도 ProjectileData/프리팹으로 교체할 수 있습니다.

## 외형 바꾸기

프리팹을 열고 자식 `Visual`의 SpriteRenderer를 편집하세요. 크기는 `Visual`의 Scale로 조정합니다. 루트 Scale은 배치 시 TotemSpawner가 설정하므로 Visual 쪽을 조정해야 합니다. 루트 BoxCollider2D는 터치 영역입니다.

실행 시에는 SO의 아이콘/회전 스프라이트 주소가 최종 그림을 결정합니다. 프리팹 Sprite만 바꾸면 실행 시 기존 그림으로 돌아올 수 있으므로, 새 그림을 Addressables에 등록하고 SO의 iconAddress와 rotationSpriteAddresses도 바꿔주세요.

## 복제해서 새 토템 만들기

1. 비슷한 번호의 SO와 프리팹을 각각 복제합니다.
2. SO의 totemId를 고유한 값으로 바꾸고 이름·설명·등급·범위를 설정합니다.
3. 복제 프리팹의 토템 컴포넌트에서 Totem Data를 새 SO로 지정합니다. TotemBase 계열 스크립트는 하나만 둡니다.
4. 복제 프리팹을 Addressables에 새 주소로 등록하고 SO의 prefabAddress에 입력합니다.
5. IngameScene의 TotemSelectPanel → Totem Pool에 새 SO를 추가합니다.
6. SO를 선택한 뒤 Assets → Totems → Validate Selected SO를 실행합니다.

게임에서 선택됐던 ID는 해당 런의 후보에서 제외됩니다. ID를 중복시키지 마세요. 검증 자동 실행은 새 Play Mode에서 `TotemContentChecks.Run()`을 사용하며 결과는 `Temp/totem-content-checks.txt`에 기록됩니다.
