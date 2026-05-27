@echo off
chcp 65001 >nul
color 0A
title GGD 프로젝트 - Rclone 자동 설정 도구

echo =======================================================
echo          GGD 프로젝트 Rclone 구글 드라이브 연동
echo =======================================================
echo.
echo 이 스크립트는 팀원들의 PC에 Rclone 전용 Client ID를
echo 자동으로 등록하고 구글 드라이브 인증을 진행합니다.
echo.
echo [주의] 시스템 환경 변수(PATH)에 rclone이 등록되어 있어야 합니다.
echo.
pause

echo.
echo [1/2] GGD 전용 Rclone 리모트(gdrive_ggd)를 생성합니다...
rclone config create gdrive_ggd drive client_id "186977969302-4qdnm0sbh6ltnr12pl3cps7v9op9tppd.apps.googleusercontent.com" client_secret "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" scope "drive"

echo.
echo [2/2] 구글 브라우저를 열어 로그인을 진행합니다...
echo.
echo =======================================================
echo [필독] 브라우저 로그인 창에서 주의사항!
echo 1. 본인의 구글 계정으로 로그인합니다.
echo 2. "Google에서 확인하지 않은 앱" 경고가 뜨면:
echo    - 왼쪽 아래 [고급] 클릭
echo    - 맨 아래 [rclone(으)로 이동(안전하지 않음)] 클릭
echo    - 권한 [허용] 클릭
echo 3. 터미널 창에 "Configure this as a Shared Drive?" 라고 나오면
echo    반드시 'n' 을 입력하고 엔터를 누르세요!
echo =======================================================
echo.
echo 브라우저 인증을 시작하려면 아무 키나 누르세요.
pause >nul

rclone config reconnect gdrive_ggd:

echo.
echo =======================================================
echo 설정이 모두 완료되었습니다!
echo 이제 유니티 상단의 [Tools - Rclone Sync Manager]를
echo 통해 에셋을 동기화하실 수 있습니다.
echo =======================================================
pause
