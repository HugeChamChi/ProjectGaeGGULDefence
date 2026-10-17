using UnityEngine;

namespace GaeGGUL.DeckUpgrade
{
    // Keeps the preview's runtime reference pointed at the production glossary and clips.
    public sealed class DeckUpgradeGlossarySettings : ScriptableObject
    {
        public TermGlossary Glossary;
        public Shader BlurShader;
    }
}
