using UnityEngine;

/// <summary>Use the bundled chart until the matching server chart has been uploaded.</summary>
public sealed class ResearchChartConfig : ScriptableObject
{
    public bool UseRemote;
    public TextAsset BundledChart;
}
