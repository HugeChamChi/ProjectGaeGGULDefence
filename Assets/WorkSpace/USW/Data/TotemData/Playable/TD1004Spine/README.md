# TD1004 Spine 외형

`SPN_TD1004_SkeletonData.asset`은 JSY_Artwork/SpineTotem의 원본 `SPN_TD1004.skel.bytes`(Spine 4.3.26)를 참조한다. 애니메이션 이름은 `animation`, 길이는 1.6초다.

원본 PNG는 PMA이며 원본 자동 생성 재질에는 텍스처 연결이 없었다. 원본 PNG를 유지한 채 `TD1004_StraightAlpha.png`의 부분 투명 픽셀 RGB를 alpha로 나누어 Straight Alpha로 변환했다. atlas 영역·좌표는 그대로 유지하고 페이지 이름과 `pma:false`만 변경했다. 재질은 `Universal Render Pipeline/Spine/Skeleton`, Straight Alpha Input을 사용한다.

`TD1004.prefab`의 `SpineVisual`은 기존 그림의 실제 보이는 높이에 맞춘 scale 0.2827878을 사용한다. `TotemSpineVisual`이 숨긴 SpriteRenderer의 색·정렬을 전달하고 선택 화면 대기 시 실제 시간으로 재생한다. 기존 소환 주기·골렘 프리팹·아이콘 주소는 동일하다.

2026-10-13 확인: 프리팹 저장 및 0/0.4/0.8/1.2초 렌더링, 세 프레임의 메시 변화, 재질·텍스처·아이콘 참조 정상. 사용자가 외형 확인만 요청하여 실제 게임 배치·드래그·40초 소환 통합 검증은 완료하지 않았다. 근거는 `outputs/td1004-spine-20261013/`.
