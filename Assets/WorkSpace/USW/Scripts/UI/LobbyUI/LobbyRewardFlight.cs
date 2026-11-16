using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 전투 결과에서 로비로 돌아온 뒤, 받은 보상(골드, 정수, 계정 경험치) 아이콘이 화면 가운데에서 퍼졌다가
/// 로비 상단 자리로 날아가는 연출. 지급은 이미 끝났고 화면만 움직인다.
/// 로비 씬을 수정하지 않도록 씬 로드 때 스스로 만들어지고, 끝나면 사라진다. 입력은 막지 않는다.
/// 로비는 일시정지가 없어 게임 시간으로 움직인다(고정 프레임 녹화와 실제 속도가 같다).
/// </summary>
public sealed class LobbyRewardFlight : MonoBehaviour
{
    private const string MainUiRoot = "# Main UI";
    private const string TopPanelPath = "MainUI_SafeAreaRoot/MainUI_TopPanel";
    private const string ProfilePath = TopPanelPath + "/ProfileLayout";
    private const string GoldPath = TopPanelPath + "/CurrencyRow/GoldCard";
    private const int SortingOrder = 4000;

    // 연출 시간(초)과 크기(화면 높이 비율). 화면 연출 전용 값이라 데이터로 빼지 않는다.
    // 레퍼런스(운빨존많겜 로비 보상 GIF, 약 1.8초): 아이콘이 하나씩 나와 잠깐 떠 있다가 줄지어 날아간다.
    private const float StartDelay = .35f;
    private const float PopSeconds = .35f;
    private const float ScatterSeconds = .7f;
    private const float HoldSeconds = .35f;
    private const float FlySeconds = .7f;
    private const float FlyStagger = .05f;
    private const float FlyPullBack = 1.3f;
    private const float EntryStagger = .22f;
    private const float IconStagger = .07f;
    private const float IconSize = .042f;
    private const float ScatterRadius = .12f;
    private const float FloatRadius = .012f;
    private static readonly Vector2 StartViewport = new(.5f, .42f);

    private BattleRewardHandoff.Entry[] _entries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !BattleRewardHandoff.HasPending) return;
        var entries = BattleRewardHandoff.Take();
        if (scene.name != "LobbyScene" && scene.name != "ShopTestLobby") return;
        var host = new GameObject(nameof(LobbyRewardFlight));
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<LobbyRewardFlight>()._entries = entries;
    }

    private void Start() => PlayAsync(this.GetCancellationTokenOnDestroy()).Forget();

    private async UniTaskVoid PlayAsync(CancellationToken token)
    {
        // 로딩창이 걷힌 뒤 시작한다.
        var root = FindFirstObjectByType<RootLifetimeScope>();
        SceneChangeManager scenes = null;
        if (root != null) root.Container.TryResolve(out scenes);
        if (scenes != null) await UniTask.WaitUntil(() => !scenes.IsTransitioning, cancellationToken: token);
        await UniTask.Delay((int)(StartDelay * 1000), cancellationToken: token);

        var mainUi = FindMainUi();
        var canvas = BuildCanvas();
        float unit = Screen.height;
        Vector2 start = new(Screen.width * StartViewport.x, Screen.height * StartViewport.y);
        // 모든 아이콘이 다 나와 퍼진 뒤 잠깐 머물렀다가, 나온 순서대로 하나씩 출발한다.
        float spawnEnd = 0f;
        for (int e = 0; e < _entries.Length; e++)
            spawnEnd = Mathf.Max(spawnEnd, e * EntryStagger + (IconCount(_entries[e].Amount) - 1) * IconStagger);
        float releaseAt = spawnEnd + ScatterSeconds + HoldSeconds;
        float last = 0f;
        int order = 0;
        for (int e = 0; e < _entries.Length; e++)
        {
            var entry = _entries[e];
            var target = FindTarget(mainUi, entry.Target);
            Vector2 end = ScreenPoint(target, new Vector2(Screen.width * .5f, Screen.height * .95f));
            int count = IconCount(entry.Amount);
            for (int i = 0; i < count; i++)
            {
                float delay = e * EntryStagger + i * IconStagger;
                float flyAt = releaseAt + order++ * FlyStagger;
                bool first = i == 0;
                last = Mathf.Max(last, flyAt + FlySeconds);
                Vector2 scatter = start + Random.insideUnitCircle * unit * ScatterRadius;
                SpawnIcon(canvas.transform, entry.Icon, unit * IconSize, start, scatter,
                    scatter + Random.insideUnitCircle * unit * FloatRadius, end, delay, flyAt,
                    () => Arrive(canvas.transform, target, end, first ? entry.Amount : 0, unit));
            }
        }
        await UniTask.Delay((int)((last + 1f) * 1000), cancellationToken: token);
        Destroy(gameObject);
    }

    private static int IconCount(int amount) => amount >= 1000 ? 10 : amount >= 100 ? 8 : 6;

    private Canvas BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        return canvas;
    }

    private static Transform FindMainUi()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == MainUiRoot) return go.transform;
        return null;
    }

    private static RectTransform FindTarget(Transform mainUi, BattleRewardHandoff.Target target)
    {
        if (mainUi == null) return null;
        string path = target switch
        {
            BattleRewardHandoff.Target.Experience => ProfilePath,
            BattleRewardHandoff.Target.Gold => GoldPath,
            _ => TopPanelPath, // 정수는 상단에 칸이 없어 상단 가운데로 보낸다(사용자 결정 2026-11-16).
        };
        return mainUi.Find(path) as RectTransform;
    }

    private static Vector2 ScreenPoint(RectTransform target, Vector2 fallback)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return fallback;
        var canvas = target.GetComponentInParent<Canvas>()?.rootCanvas;
        var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
    }

    private static void SpawnIcon(Transform parent, Sprite sprite, float size, Vector2 start, Vector2 scatter, Vector2 hover,
        Vector2 end, float delay, float flyAt, TweenCallback onArrive)
    {
        var go = new GameObject("RewardIcon", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(size, size);
        rect.position = start;
        rect.localScale = Vector3.zero;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        DOTween.Sequence().SetLink(go)
            .AppendInterval(delay)
            .Append(rect.DOScale(1f, PopSeconds).SetEase(Ease.OutBack))
            .Join(rect.DOMove(scatter, ScatterSeconds).SetEase(Ease.OutCubic))
            // 출발 전까지 제자리에서 살짝 떠다닌다.
            .Append(rect.DOMove(hover, Mathf.Max(.01f, flyAt - delay - ScatterSeconds)).SetEase(Ease.InOutSine))
            // 살짝 뒤로 당겼다가 가속해서 날아간다.
            .Append(rect.DOMove(end, FlySeconds).SetEase(Ease.InBack, FlyPullBack))
            .Join(rect.DOScale(.7f, FlySeconds).SetEase(Ease.InQuad))
            .OnComplete(() => { onArrive?.Invoke(); Destroy(go); });
    }

    private static void Arrive(Transform parent, RectTransform target, Vector2 point, int amount, float unit)
    {
        if (target != null && target.gameObject.activeInHierarchy)
        {
            // 아이콘마다 칸이 튀므로 이전 튐을 끝내고 원래 크기에서 다시 튄다.
            target.DOComplete();
            target.DOPunchScale(Vector3.one * .1f, .25f, 6, .6f).SetLink(target.gameObject);
        }
        if (amount <= 0) return;
        var go = new GameObject("RewardAmount", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(unit * .3f, unit * .05f);
        rect.position = point + new Vector2(0f, -unit * .045f);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = "+" + amount.ToString("N0");
        text.fontSize = unit * .028f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineWidth = .25f;
        text.outlineColor = new Color32(40, 24, 8, 255);
        text.raycastTarget = false;
        DOTween.Sequence().SetLink(go)
            .Append(rect.DOMoveY(rect.position.y - unit * .02f, .7f).SetEase(Ease.OutCubic))
            .Insert(.4f, text.DOFade(0f, .3f))
            .OnComplete(() => Destroy(go));
    }
}
