using System;

namespace GGD.UI.Authoring
{
    /// <summary>Typed catalog keys connect the same rendered document to mock or production services.</summary>
    public interface IAuthoringBindings
    {
        /// <summary>Refreshes controls silently after service state changes.</summary>
        event Action OnChanged;
        /// <summary>Reads a registered scalar state key.</summary>
        object Read(string key);
        /// <summary>Requests one registered command; implementations report failure instead of faking success.</summary>
        void Execute(string key, object argument = null);
    }
}
