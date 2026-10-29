using System;

namespace GGD.UI.External
{
    /// <summary>State and command boundary shared by preview and game-specific adapters.</summary>
    public interface IUiBindings
    {
        /// <summary>Notifies views after state changes; initial rendering must not issue commands.</summary>
        event Action OnChanged;
        /// <summary>Reads an explicitly registered, scalar display value.</summary>
        object Read(string key);
        /// <summary>Checks the binding and optional command argument type before creating a view.</summary>
        void Validate(string key, string command, Type argumentType);
        /// <summary>Dispatches an explicitly registered command.</summary>
        void Execute(string command, object argument = null);
    }
}
