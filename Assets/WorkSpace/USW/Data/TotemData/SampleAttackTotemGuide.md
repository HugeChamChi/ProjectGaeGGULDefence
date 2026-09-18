# 샘플 공격 토템 만들기

`SampleAttackTotem.prefab`과 `SampleAttackTotemData.asset`이 한 쌍입니다.
현재 IngameScene의 TotemSelectPanel > Totem Pool에 SO를 등록했습니다.
보스 처치 후 샘플 카드 1장이 나옵니다. 선택한 ID는 해당 런에서 다시 나오지 않습니다.

## 현재 설정

- Normal / ID 99001 / Use Sheet Data 끔.
- Functions: SimpleBuffFunction, AttackPercent, Amount **0.1 = 10%**.
- Effect Ranges: TotemRelativeOffsetRange, (-1, 0), (1, 0).
- Attack Disabled Ranges: 비어 있음.
- Is Rotatable: 켬. 회전하면 효과 범위도 회전합니다.
- Prefab Address: `SampleAttackTotem`.
- Icon Address 및 Rotation Sprite Addresses 4개: `TotemSampleAttackIcon`.
- 외형은 기존 노말 토템 아이콘 테두리를 임시로 재사용합니다. 전용 그림은 교체하세요.
- 프리팹 구성: GenericBuffTotem, SpriteRenderer, DragHandler, BoxCollider2D.

## 복제 순서

1. Edit Mode에서 SO와 프리팹을 각각 복제하고 이름을 바꿉니다.
2. 새 SO의 Totem Id를 고유한 값으로 변경합니다. 이름·설명·등급·Functions·Effect Ranges를 수정합니다. Use Sheet Data는 끕니다.
3. 새 프리팹의 GenericBuffTotem > Totem Data에 새 SO를 연결합니다. Sprite Renderer 참조는 유지합니다.
4. 새 프리팹을 Addressable로 등록하고 고유한 Address를 지정합니다. 그 문자열을 새 SO의 Prefab Address에 입력합니다.
5. 새 스프라이트도 Addressable로 등록합니다. 새 SO의 Icon Address와 Rotation Sprite Addresses에 주소를 넣고, 프리팹의 SpriteRenderer > Sprite에도 연결합니다. 회전 그림이 하나면 4칸 모두 같은 주소를 사용하세요.
6. 이미지 크기에 맞게 프리팹의 BoxCollider2D 크기·Offset을 조절합니다. 실제 소환 크기는 씬의 TotemSpawner > Spawn Scale도 적용됩니다.
7. 인게임 씬의 TotemSelectPanel > Totem Pool에 새 SO를 추가합니다. 기존 목록을 덮어쓰지 마세요.
8. SO 선택 후 Assets > Totems > Validate Selected SO로 주소·동작 구성을 검사합니다.

Play Mode에서 불러온 icon/prefab/rotationSprites는 런타임 캐시입니다. 샘플 파일에는 캐시를 비워 두었으므로 Edit Mode에서 복제해 Address 필드를 기준으로 설정하세요.
단순 공격력·공속 버프는 GenericBuffTotem과 SO만으로 만들 수 있습니다. 주기적 소환·특수 공격 토템은 해당 기능을 구현한 전용 TotemBase 파생 컴포넌트를 사용해야 합니다.
