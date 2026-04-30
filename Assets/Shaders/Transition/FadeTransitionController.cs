using UnityEngine;
using UnityEngine.UI;

public class FadeTransitionController : MonoBehaviour
{
    public Material transitionMaterial;
    public float duration;
    [Range(0, 1)]
    public float progress = 0;

    void Update()
    {
        if (transitionMaterial != null)
        {
            transitionMaterial.SetFloat("_Progress", progress);
        }
    }

    [ContextMenu("Test In")]
    public void TestIn()
    {
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(0, 0.5f, duration));
    }

    [ContextMenu("Test Out")]
    public void TestOut()
    {
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(0.5f, 1.0f, duration));
    }

    private System.Collections.IEnumerator TransitionRoutine(float start, float end, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            progress = Mathf.Lerp(start, end, elapsed / duration);
            if (transitionMaterial != null)
            {
                transitionMaterial.SetFloat("_Progress", progress);
            }
            yield return null;
        }
        progress = end;
        
    }
}
