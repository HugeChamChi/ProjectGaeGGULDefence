using UnityEngine;

/// <summary>Isolated authoring samples; never rewrites the gameplay catalog or shared glossary.</summary>
[CreateAssetMenu(menuName = "Game/UI/Lab/Description Mode Samples")]
public sealed class DescriptionModeLabData : ScriptableObject
{
    public TermGlossary Glossary;
    public DescriptionModeLabEntry[] Selections;
    public DescriptionModeLabEntry[] Totems;
}
