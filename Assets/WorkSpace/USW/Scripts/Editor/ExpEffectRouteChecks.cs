using System;
using System.Collections;
using System.Reflection;
using AssetKits.ParticleImage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>경로 방향, 실제 ParticleImage 추적, 경험치 단발 지급을 격리된 씬에서 검증한다.</summary>
public static class ExpEffectRouteChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _checks;

    /// <summary>현재 게임/씬 상태를 바꾸지 않고 경험치 연출 검사를 실행한다.</summary>
    [MenuItem("Tools/EXP Effect/Run Route Checks")]
    public static void Run()
    {
        _checks = 0;
        var randomState = UnityEngine.Random.state;
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        IList flights = null;
        try
        {
            var route = new ExpEffectRoute();
            Vector2 start = new Vector2(0.5f, 0.73f), end = new Vector2(0.47f, 0.075f);
            Check(route.Evaluate(0f, start, end, false) == start, "starts at boss");
            Check(route.Evaluate(1f, start, end, false) == end, "ends at EXP");
            Check(route.GetPoint(1, start, end, false).x < start.x && route.GetPoint(1, start, end, true).x > start.x, "immediate left/right departure");
            Check(Mathf.Approximately(route.GetPoint(1, start, end, false).y, start.y), "horizontal departure tangent");
            Check(route.GetPoint(3, start, end, false).x < 0f && route.GetPoint(3, start, end, true).x > 1f, "both sides off screen");
            Check(route.GetPoint(6, start, end, false).y < 0f, "passes below screen");
            Check(route.GetPoint(8, start, end, false).y < end.y && Mathf.Approximately(route.GetPoint(8, start, end, false).x, end.x), "vertical arrival from below");
            var right = new ExpEffectRoute();
            right.CopyMirroredFrom(route);
            Check(Vector2.Distance(right.Evaluate(0.45f, start, end, false), route.Evaluate(0.45f, start, end, true)) < 0.0001f, "independent right copy preserves mirror");
            Vector2 anchor = route.GetPoint(3, start, end, false), tangent = route.GetPoint(4, start, end, false);
            route.SetPoint(3, anchor + Vector2.up * 0.1f, start, end, false);
            Check(Vector2.Distance(route.GetPoint(4, start, end, false), tangent + Vector2.up * 0.1f) < 0.0001f, "anchor moves adjoining handles");

            root = new GameObject("EXP Route Checks", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            RectTransform canvas = (RectTransform)root.transform;
            canvas.sizeDelta = new Vector2(1080f, 2340f);
            GameObject host = new GameObject("Controller", typeof(RectTransform));
            host.transform.SetParent(root.transform, false);
            ExpEffectController controller = host.AddComponent<ExpEffectController>();
            Set(controller, "_routeCanvas", canvas);
            Set(controller, "spawnPoint", host.transform);
            Set(controller, "attractorTarget", host.transform);
            foreach (Vector2 size in new[] {new Vector2(1080f, 1920f), new Vector2(1080f, 2340f), new Vector2(1440f, 2560f)})
            {
                canvas.sizeDelta = size;
                Vector2 outside = new Vector2(-0.1f, -0.04f);
                Check(Vector2.Distance(outside, controller.WorldToNormalized(controller.NormalizedToWorld(outside))) < 0.0001f, "canvas roundtrip " + size);
            }

            ParticleImage prefab = AssetDatabase.LoadAssetAtPath<ParticleImage>("Assets/WorkSpace/HSD/Prefab/UI/Effect/ExpEffect.prefab");
            ParticleImage particle = UnityEngine.Object.Instantiate(prefab, canvas);
            particle.multithreadEnabled = false;
            object[] prepare = {particle, start, 1.15f, null};
            typeof(ExpEffectController).GetMethod("PrepareParticle", Private).Invoke(controller, prepare);
            RectTransform follower = (RectTransform)prepare[3];
            Check(particle.particleCount == 1, "one visible particle from burst");
            Check(particle.startSize.mainCurve.Evaluate(0f, 0.5f) >= 25f, "visible start size");
            Check(Vector2.Distance(particle.particles[0].Position, Vector2.zero) < 0.01f, "no initial streak from canvas center");

            Type flightType = typeof(ExpEffectController).GetNestedType("Flight", BindingFlags.NonPublic);
            object flight = Activator.CreateInstance(flightType, true);
            flightType.GetField("Target").SetValue(flight, follower);
            flightType.GetField("Duration").SetValue(flight, 1.15f);
            flightType.GetField("TailDuration").SetValue(flight, 10f);
            flightType.GetField("Reward").SetValue(flight, 0.2f);
            var samples = (Vector2[])flightType.GetField("Samples").GetValue(flight);
            var distances = (float[])flightType.GetField("Distances").GetValue(flight);
            MethodInfo randomRoute = typeof(ExpEffectController).GetMethod("BakeRandomRoute", Private);
            UnityEngine.Random.InitState(1721);
            int leftCount = 0, rightCount = 0;
            bool validRoutes = true;
            for (int i = 0; i < 200; i++)
            {
                randomRoute.Invoke(controller, new object[] {flight, start, end});
                if (samples[1].x < start.x) leftCount++; else rightCount++;
                validRoutes &= samples[0] == start && samples[samples.Length - 1] == end;
            }
            Check(leftCount > 60 && rightCount > 60 && leftCount + rightCount == 200, "one random side per flight, both directions reachable");
            Check(validRoutes, "every random route preserves start and arrival");
            Set(controller, "_mirrorRight", false);
            Set(controller, "_rightRoute", right);
            bool independentRight = false;
            for (int i = 0; i < 40; i++)
            {
                randomRoute.Invoke(controller, new object[] {flight, start, end});
                if (samples[1].x > start.x)
                    independentRight |= Vector2.Distance(samples[48], right.Evaluate(0.5f, start, end, false)) < 0.0001f;
            }
            Check(independentRight, "random right uses independently authored route");
            typeof(ExpEffectController).GetMethod("Bake", Private).Invoke(controller, new object[] {new ExpEffectRoute(), false, start, end, samples, distances});
            flights = (IList)typeof(ExpEffectController).GetField("_flights", Private).GetValue(controller);
            flights.Add(flight);
            ExpManager manager = host.AddComponent<ExpManager>();
            Set(controller, "_expManager", manager);
            int payments = 0;
            manager.OnExpChanged += _ => payments++;
            MethodInfo advance = typeof(ExpEffectController).GetMethod("AdvanceFlights", Private);
            advance.Invoke(controller, new object[] {0.575f});
            Check(payments == 0, "no reward mid-flight");
            Vector3 mid = follower.position;
            advance.Invoke(controller, new object[] {0f});
            Check(follower.position == mid, "pause holds position");
            typeof(ParticleImage).GetMethod("Simulate", Private).Invoke(particle, new object[] {0.01f, false});
            Check(Vector3.Distance(particle.transform.TransformPoint(particle.particles[0].Position), follower.position) < 0.01f, "real particle follows spatial target");
            advance.Invoke(controller, new object[] {0.575f});
            Check(payments == 1 && Mathf.Approximately(manager.CurrentExp, 0.2f), "one reward at arrival");
            Check(Vector3.Distance(follower.position, controller.NormalizedToWorld(end)) < 0.01f, "follower reaches exact destination");
            advance.Invoke(controller, new object[] {0.01f});
            Check(payments == 1, "no repeated reward during trail fade");
            typeof(ExpEffectController).GetMethod("PrepareParticle", Private).Invoke(controller, prepare);
            Check(particle.particleCount == 1, "reuse clears old particles without adding bursts");
            particle.Stop(true);
            flights.Clear();
            Set(controller, "_accumulatedExp", 0.1f);
            host.SetActive(false);
            // 일반 MonoBehaviour의 수명 콜백은 Edit Mode 임시 씬에서 자동 실행되지 않는다.
            typeof(ExpEffectController).GetMethod("OnDisable", Private).Invoke(controller, null);
            Check(payments == 2 && Mathf.Approximately(manager.CurrentExp, 0.3f), "disable preserves accumulated reward");
            controller.TestFireEffect();
            controller.PreviewEffect();
            Check(payments == 2, "edit-mode preview/test never grants EXP");
            ParticleImage self = host.AddComponent<ParticleImage>();
            Set(controller, "particleImage", self);
            MethodInfo spawn = typeof(ExpEffectController).GetMethod("SpawnParticleAndApplyExp", Private);
            spawn.Invoke(controller, new object[] {0.1f});
            Check(flights.Count == 0 && Mathf.Approximately(manager.CurrentExp, 0.4f), "self reference cannot recursively clone controller or lose reward");
            Set(controller, "particleImage", null);
            spawn.Invoke(controller, new object[] {0.1f});
            Check(Mathf.Approximately(manager.CurrentExp, 0.5f), "missing visual does not lose EXP");
            Debug.Log("[ExpEffectRouteChecks] PASS " + _checks);
        }
        finally
        {
            flights?.Clear();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Random.state = randomState;
        }
    }

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("EXP route check failed: " + message);
        _checks++;
    }
}
