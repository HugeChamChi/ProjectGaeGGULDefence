# Unit Parade Transition

`TransitionTestScene.unity`를 열고 Play하면 마지막 행렬 시안이 재생됩니다.

- 꿀색 원형 셀이 왼쪽 위에서 오른쪽 아래로 화면을 채웁니다.
- 배경 벌집 아이콘은 오른쪽 위로 흐릅니다. 이전 대비 가로·세로 간격은 절반(약 4배 개수), 화면상 이동 속도는 3배입니다.
- 알팡·베탕·델탕이 오른쪽을 보고 한 줄로 이동합니다. 뒤의 산 두 겹과 지면은 왼쪽으로 지나갑니다.
- 아래 진행바가 100%가 되면 잠깐 완성 상태를 보여준 뒤 유닛이 오른쪽으로 가속하며 퇴장하고 Out으로 끝납니다.

이전 꽃·잎·구름·점프·공중 순찰 시안과 선택 UI는 제거했습니다. Full cycle / IN / Hold / OUT / Clear, 반복·진행도·시간 조절만 남아 있습니다.

이 씬의 진행바는 Hold 시간에 맞춘 시안용 진행도입니다. 게임 공용 FadeCanvas나 로비에는 적용하지 않았고 빌드 목록에도 추가하지 않았습니다. 전역 VContainer 부트스트랩은 기존 설정을 따릅니다.

유닛은 프로젝트의 기존 `Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Spine_Artwork` 알팡·베탕·델탕 Spine 데이터를 참조합니다. 원본 idle에 위치·회전·스케일 모션을 더했습니다. Imports 원본 아트는 저장소의 기존 별도 관리 규칙을 따르므로 해당 아트가 있는 프로젝트에서 실행합니다.

## 검증

Unity Pipeline으로 `Tools/verification/TransitionConceptSceneSetup.cs`를 실행합니다.

- `Build`: 테스트 씬 구성
- `CheckVisuals`: 가로·세로·정사각형 화면 끝점과 대각선 방향, 아이콘 밀도·스크롤 변위 검사
- `RecordParade` (Play 모드): 진행바 증가, 완료 후 출발, 오른쪽 방향·퇴장과 투명 종료 검사 및 실제 화면 녹화
