using GGD.UI.External;

namespace GGD.UI.Preview
{
    /// <summary>Scene-owned preview session; staged bindings become current only on successful view construction.</summary>
    public sealed class PreviewSession
    {
        /// <summary>Current temporary state.</summary>
        public PreviewBindings Current { get; private set; }
        /// <summary>Total dispatched commands, allowing duplicate-listener checks across reloads.</summary>
        public int CommandCount { get; private set; }
        /// <summary>Last preview command for the diagnostic toolbar.</summary>
        public string LastCommand { get; private set; } = "none";

        /// <summary>Creates isolated candidate state without changing the displayed session.</summary>
        public PreviewBindings Prepare(UiContentPackage package, bool reset) =>
            PreviewBindings.Prepare(package, reset ? null : Current, Record);

        /// <summary>Commits an already prepared candidate.</summary>
        public void Commit(PreviewBindings bindings) { Current = bindings; }
        private void Record(string command) { CommandCount++; LastCommand = command; }
    }
}
