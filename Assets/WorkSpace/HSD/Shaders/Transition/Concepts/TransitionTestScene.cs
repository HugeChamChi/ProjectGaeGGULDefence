using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Isolated motion playground. No game services, loading, or persistent objects.</summary>
public sealed class TransitionTestScene : MonoBehaviour
{
    [SerializeField] private Material transitionMaterial;
    [SerializeField] private RawImage preview;
    [SerializeField] private TransitionSilhouetteShowcase showcase;
    private Material material;
    private float duration = 1.2f;
    private float hold = 4.2f;
    private bool loop;
    public float Progress { get; private set; } = 1f;
    public bool IsRunning { get; private set; }

    public bool Loop { get => loop; set => loop=value; }
    private void Start()
    {
        material = Instantiate(transitionMaterial);
        material.SetFloat("_Preview", 1);
        preview.material = material;
        PlayCycle();
    }
    public void Hold()
    {
        Stop();
        Progress = .5f;
        showcase.ResetPreview();
    }
    private void Update()
    {
        if (material == null) return;
        material.SetFloat("_Progress", Progress);
        material.SetFloat("_Clock", Time.unscaledTime);
        material.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
        showcase.SetProgress(Progress);
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
    public void Stop() { StopAllCoroutines(); IsRunning = false; if(showcase!=null) showcase.CancelExit(); }
    public void PlayCycle() { Stop(); StartCoroutine(Cycle()); }
    public void PlayIn() { Stop(); showcase.ResetPreview(); StartCoroutine(Single(0, .5f)); }
    public void PlayOut() { Stop(); StartCoroutine(Exit()); }
    private IEnumerator Exit()
    {
        IsRunning=true;
        Progress=.5f;
        showcase.BeginExit(duration+.25f);
        yield return new WaitForSecondsRealtime(.25f);
        yield return Animate(.5f,1);
        IsRunning=false;
    }
    private IEnumerator Single(float from, float to)
    {
        IsRunning = true;
        yield return Animate(from, to);
        IsRunning = false;
    }
    private IEnumerator Animate(float from, float to)
    {
        float elapsed = 0;
        Progress = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Progress = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        Progress = to;
    }
    private IEnumerator Cycle()
    {
        IsRunning = true;
        do
        {
            showcase.ResetPreview();
            yield return Animate(0, .5f);
            float elapsed=0;
            while(elapsed<hold)
            {
                elapsed+=Time.unscaledDeltaTime;
                showcase.SetLoading(elapsed/hold);
                yield return null;
            }
            showcase.SetLoading(1);
            yield return new WaitForSecondsRealtime(.2f);
            yield return Exit();
            IsRunning=true;
            yield return new WaitForSecondsRealtime(.4f);
        } while (loop);
        IsRunning = false;
    }
    private void OnGUI()
    {
        float scale = Mathf.Max(.5f, Mathf.Min(Screen.width / 560f, Screen.height / 700f));
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        GUILayout.BeginArea(new Rect(12, height - 255, width - 24, 243), GUI.skin.box);
        GUILayout.Label("TRANSITION LAB / UNIT PARADE");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Full cycle", GUILayout.Height(32))) PlayCycle();
        if (GUILayout.Button("IN", GUILayout.Height(32))) PlayIn();
        if (GUILayout.Button("Hold", GUILayout.Height(32))) Hold();
        if (GUILayout.Button("OUT", GUILayout.Height(32))) PlayOut();
        if (GUILayout.Button("Clear", GUILayout.Height(32))) { Stop(); Progress = 1; }
        GUILayout.EndHorizontal();
        loop = GUILayout.Toggle(loop, "Loop full cycle");
        GUILayout.Label("Scrub: " + Progress.ToString("0.00") + "   (0 clear / 0.5 covered / 1 clear)");
        float value = GUILayout.HorizontalSlider(Progress, 0, 1);
        if (!Mathf.Approximately(value, Progress)) { Stop(); Progress = value; }
        GUILayout.BeginHorizontal();
        GUILayout.Label("Speed: " + duration.ToString("0.0") + "s", GUILayout.Width(100));
        duration = GUILayout.HorizontalSlider(duration, .3f, 3);
        GUILayout.Label("Hold: " + hold.ToString("0.0") + "s", GUILayout.Width(90));
        hold = GUILayout.HorizontalSlider(hold, .2f, 5);
        GUILayout.EndHorizontal();
        GUILayout.Label("Circles: top-left > bottom-right / Icons: upper-right");
        GUILayout.EndArea();
    }
}
