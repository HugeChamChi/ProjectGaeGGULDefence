using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>실제 TD1001~1010 Addressables와 배치/화염구/생산 연결을 새 Play Mode에서 확인한다.</summary>
public static class TotemContentChecks
{
    /// <summary>약 43초간 실제 전투를 진행하고 Temp에 결과를 쓴다. 테스트 후 Play Mode를 종료한다.</summary>
    public static async UniTaskVoid Run()
    {
        var results = new List<string>();
        void Check(bool ok, string name)
        {
            if (!ok) throw new Exception(name);
            results.Add("PASS " + name);
            System.IO.File.WriteAllLines("Temp/totem-content-checks.txt", results);
        }
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("Requires fresh Play Mode");
            var scope = UnityEngine.Object.FindFirstObjectByType<InGameLifetimeScope>();
            var token = scope.GetCancellationTokenOnDestroy();
            var resolver = scope.Container;
            var grid = (GridManager)resolver.Resolve(typeof(GridManager));
            var spawner = (TotemSpawner)resolver.Resolve(typeof(TotemSpawner));
            var game = (GameManager)resolver.Resolve(typeof(GameManager));
            var bosses = (BossManager)resolver.Resolve(typeof(BossManager));
            var currency = (CurrencyManager)resolver.Resolve(typeof(CurrencyManager));
            var spawned = new List<TotemBase>();
            for (int id = 1001; id <= 1010; id++)
            {
                var data = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TotemData>(
                    "Assets/WorkSpace/USW/Data/TotemData/Playable/TD" + id + "Data.asset"));
                await data.LoadAssetsAsync().AttachExternalCancellation(token);
                Check(data.prefab != null && data.icon != null, "TD" + id + " loads prefab/icon");
                GridCell target = null;
                foreach (var cell in grid.AllCells()) if (cell.IsAvailable) { target = cell; break; }
                Check(await spawner.PlaceTotemAtCellAsync(data, target, token), "TD" + id + " places through runtime spawner");
                var totem = target.OccupyingTotem;
                spawned.Add(totem);
                Check(totem.GetComponents<TotemBase>().Length == 1 && totem.GetComponentInChildren<SpriteRenderer>().sprite == data.icon,
                    "TD" + id + " has one behavior and matching numbered artwork");
            }
            game.OnStartButtonPressed();
            await UniTask.WaitUntil(() => bosses.CurrentBoss != null, cancellationToken: token).Timeout(TimeSpan.FromSeconds(15));
            var boss = bosses.CurrentBoss;
            float food = currency.Currency;
            await UniTask.Delay(TimeSpan.FromSeconds(6), cancellationToken: token);
            bool burning = false;
            foreach (var debuff in boss.Debuffs.Active) if (debuff.Definition.Kind == DebuffKind.Burn) burning = true;
            Check(burning, "TD1003 actual projectile applies burn to live boss");
            await UniTask.Delay(TimeSpan.FromSeconds(36), cancellationToken: token);
            Check(currency.Currency >= food + 120f, "TD1010 produces food through actual timed loop");
            Check(UnityEngine.Object.FindFirstObjectByType<WildcardUnit>() != null, "TD1004 loads wildcard and creates actual unit after 40 seconds");
            foreach (var totem in spawned) Check(totem != null && totem.IsActive, "Placed totem survives runtime checks");
            ScreenCapture.CaptureScreenshot("Temp/totem-content.png");
            results.Add("COMPLETE " + results.Count);
            System.IO.File.WriteAllLines("Temp/totem-content-checks.txt", results);
        }
        catch (Exception e)
        {
            results.Add("FAIL " + e);
            System.IO.File.WriteAllLines("Temp/totem-content-checks.txt", results);
            Debug.LogException(e);
        }
    }
}
