using VContainer;
using VContainer.Unity;

namespace GGD.UI.Preview
{
    /// <summary>Dedicated preview entry point. Its generated project contains no production root settings or services.</summary>
    public sealed class PreviewLifetimeScope : LifetimeScope
    {
        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<PreviewSession>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<PreviewPlayer>();
        }
    }
}
