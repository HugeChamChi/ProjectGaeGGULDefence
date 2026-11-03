# 강화 서버 저장 연결 결과

2026-11-01. 기존 PlayerData 행의 UpgradeProgressJson으로 영구 강화 서버 저장을 연결했다. 현재 강화는 ResearchProgress.GetUpgradeCost가 0인 무료 기능이며 재화 소비를 추가하지 않았다.

## 연결

- UpgradeProgressData는 저장 버전, 트리별 ResearchSaveData, 이관 ID를 보관한다. 기존 컬럼 누락은 빈 진행으로 호환하고 손상/미지원 버전/중복 트리·노드/음수 레벨은 거절한다.
- RootLifetimeScope의 IResearchSaveStore 등록을 AccountResearchSaveStore로 교체했다. ResearchAccountContext는 로드된 BACKND 계정에 연결하고 로그아웃/계정 전환 시 세션 캐시를 폐기한다.
- 각 강화 변경은 기존 PlayerData 저널에 동기 기록한다. 무료 변경은 기존 30초 묶음/최대60초 flush를 공유한다. 화면의 저장 상태는 PlayerData의 서버 확인 상태를 표시한다.
- 명시적 Flush, 로비 복귀, 출격은 공통 snapshot 서버 확인을 기다린다. 실패하면 진행/저널을 유지하고 출격을 보류한다. 계정 전환은 기존 PlayerData 저장 확인을 공유한다.
- 소유자가 맞는 계정별 로컬 키는 해당 서버 트리가 없을 때 이관한다. 서버 확인 전 실패하면 서버 연결 로드를 완료하지 않는다. 원래 로컬 데이터는 삭제하지 않는다.
- 계정 구분 없는 local 키는 자동 귀속하지 않는다. 해당 진행은 보존되어 있으며, 현재 로그인 계정에 귀속할지 사용자의 선택이 필요하다. 서버 연결 자체는 로그인 계정의 새/기존 진행으로 동작한다.

## 검증

- Unity 컴파일 오류 0.
- 기존 SaveReliabilityChecks.Run 회귀 27 PASS(실제 15초 timeout 포함).
- UpgradeServerSaveChecks.Run: 실제 PlayerDataController와 격리 backend로 16 PASS. 즉시 저널, 복사본 격리, 재시작/실패 복구, clean no-op, 무료 재화 유지, 트리·계정 격리, 손상 저장 차단.
- 실제 서버 검사 PASS: 운영 DI 등록과 인증 계정 연결, UpgradeProgressJson 컬럼 저장/재조회, 효과가 없는 고유 검증용 트리의 노드 2레벨 저장/읽기 및 제거를 확인했다. 실제 밸런스 노드와 재화는 변경하지 않았다.
- 첫 노드 왕복 검사에서 LitJson의 ResearchSaveData.CurrentVersion 상수 필드 직렬화/역직렬화 오류를 발견했다. UpgradeProgressJson에 JsonUtility를 사용하고 구조를 검사하도록 수정했다. 격리 검사 재실행 통과.

검사 파일: tools/verification/UpgradeServerSaveChecks.cs. Run은 격리 backend만 사용한다. Live는 기존 로그인/로드 완료 세션이 필요하며 실제 밸런스 노드와 재화는 변경하지 않는다.

서버는 현재 구조의 최종 확인본, 로컬은 미확인 변경 복구 기록이다. 앱 데이터 삭제 전 아직 전달되지 않은 기록, 구버전 클라이언트와의 동시 사용, 다기기 완전 동시 요청의 DB 원자성까지 보장한다는 의미는 아니다.
