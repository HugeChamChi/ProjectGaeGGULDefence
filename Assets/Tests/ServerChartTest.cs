using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using GGD.BackendCharts;
using GGD.Events;
using UnityEngine;
using VContainer;

// DEV chart inspection plus an explicit production title initialization check.
public sealed class ServerChartTest : MonoBehaviour
{
    private string user = "", password = "", output = "Play → 로그인 → DEV 차트 받기";
    private bool busy;
    private Vector2 scroll;
    private GUISkin largeSkin;
    public string Result => output;

    private void OnGUI()
    {
        if (largeSkin == null) largeSkin = Instantiate(GUI.skin);
        GUI.skin = largeSkin;
        int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 28f), 24, 48);
        GUI.skin.label.fontSize = fontSize;
        GUI.skin.label.wordWrap = true;
        GUI.skin.button.fontSize = fontSize;
        GUI.skin.button.fixedHeight = fontSize * 2.2f;
        GUI.skin.textField.fontSize = fontSize;
        GUI.skin.textField.fixedHeight = fontSize * 2f;
        GUI.skin.verticalScrollbar.fixedWidth = 28;
        GUI.skin.verticalScrollbarThumb.fixedWidth = 28;
        GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("서버 차트 / 타이틀 초기화 테스트 · DEV");
        GUILayout.Label("로그인: " + Backend.IsLogin + "   기존 토큰 또는 테스트 계정 사용 (자동 가입 없음)");
        GUI.enabled = !busy;
        if (GUILayout.Button("실제 타이틀 초기화 실행 (계정 로드·일일 갱신 포함)")) InitializeTitle().Forget();
        user = GUILayout.TextField(user);
        password = GUILayout.PasswordField(password, '*');
        if (GUILayout.Button("기존 토큰 로그인")) Login(true).Forget();
        if (GUILayout.Button("입력 계정 로그인")) Login(false).Forget();
        GUI.enabled = !busy && Backend.IsLogin;
        if (GUILayout.Button("DEV 유닛 기본 스탯 받기 / 검증")) Download(true).Forget();
        if (GUILayout.Button("DEV 미션 이벤트 받기 / 검증")) Download(false).Forget();
        GUI.enabled = true;
        GUILayout.Label(output);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    public async UniTask InitializeTitle()
    {
        if (busy) return;
        busy = true;
        output = "타이틀과 같은 AppInitialization 경로 실행 중…";
        try
        {
            var root = FindFirstObjectByType<RootLifetimeScope>();
            if (root == null || root.Container == null)
                throw new InvalidOperationException("RootLifetimeScope가 필요합니다. 프로젝트 루트 DI 설정을 확인하세요.");
            var initialization = root.Container.Resolve<AppInitialization>();
            await initialization.InitializeAsync(this.GetCancellationTokenOnDestroy());
            if (!initialization.IsReady || !Player.PlayerData.IsReady)
                throw new InvalidOperationException("타이틀 초기화 완료 상태가 아닙니다.");
            output = "타이틀 초기화 성공: 로그인 → 테이블 → 게임 데이터 → 계정 데이터\n";
            foreach (var name in new[] {"UnitBalanceChartSettings", "EventChartSettings"})
            {
                var settings = Resources.Load<TextAsset>(name);
                output += name + ": " + (settings == null ? "설정 없음" : settings.text) + "\n";
            }
            output += "UseRemote=false이면 포함 데이터 모드입니다. 초기화 성공은 미구현 강화 구매·이벤트 지급의 완료를 의미하지 않습니다.";
        }
        catch (Exception e) { output = "타이틀 초기화 실패: " + e.Message; }
        finally { busy = false; }
    }

    private void OnDestroy()
    {
        if (largeSkin != null) Destroy(largeSkin);
    }

    public async UniTask Login(bool tokenLogin)
    {
        busy = true; output = "로그인 중…";
        try
        {
            if (!Backend.IsLogin)
            {
                var init = Backend.Initialize();
                if (!init.IsSuccess()) throw new InvalidOperationException("Initialize: " + init.GetStatusCode());
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime);
                var completion = new UniTaskCompletionSource<BackendReturnObject>();
                if (tokenLogin) Backend.BMember.LoginWithTheBackendToken(r => completion.TrySetResult(r));
                else Backend.BMember.CustomLogin(user, password, r => completion.TrySetResult(r));
                var response = await completion.Task.AttachExternalCancellation(timeout.Token);
                if (!response.IsSuccess()) throw new InvalidOperationException("Login: " + response.GetStatusCode());
            }
            output = "로그인 성공. DEV 차트를 선택하세요.";
        }
        catch (Exception e) { output = "실패: " + e.Message; }
        finally { password = ""; busy = false; }
    }

    public async UniTask Download(bool units)
    {
        busy = true; output = "서버에서 받는 중…";
        string chart = units ? "DEV_UnitBaseStats" : "DEV_MissionEvents";
        try
        {
            string owner = Backend.UserInDate;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime);
            var rows = await BackendChartReader.ReadAsync(chart, timeout.Token);
            if (!Backend.IsLogin || owner != Backend.UserInDate) throw new OperationCanceledException("Account changed");
            string json = ChartSnapshotPayload.Decode(rows.ToJson());
            string summary;
            if (units)
            {
                var config = UnitBalanceSnapshot.ParseConfig(json);
                using var snapshot = new UnitBalanceSnapshot(config);
                summary = "유닛 " + snapshot.Count + "개 검증 성공\n";
                foreach (var unit in config.Units)
                    foreach (var stat in unit.Stats)
                        summary += $"{unit.Name} / {stat.Tier}: 공격 {stat.Attack}, 회/초 {stat.AttackFrequency}, 쿨타임 {stat.SkillCooldown}, 식량 {stat.FoodProduction}, 드론 {stat.MaxDroneCount}\n";
            }
            else
            {
                var config = JsonUtility.FromJson<EventDataConfig>(json);
                _ = new EventDataRuntime(owner, config, DailyManager.ResetHourKst);
                summary = "미션 설정 검증 성공\n" + json;
            }
            output = chart + " · 서버 다운로드 / 해시 / 설정 검증 성공\n" + summary;
        }
        catch (Exception e) { output = chart + " 실패: " + e.Message; }
        finally { busy = false; }
    }
}
