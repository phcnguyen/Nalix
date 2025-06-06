using Nalix.Network.Web.Http;
using Nalix.Network.Web.Internal;
using Nalix.Network.Web.MimeTypes;
using Nalix.Network.Web.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Web.WebModule;

/// <summary>
/// <para>Groups modules under a common base URL path.</para>
/// <para>The <see cref="IWebModule.BaseRoute">BaseRoute</see> property
/// of modules contained in a <c>ModuleGroup</c> is relative to the
/// <c>ModuleGroup</c>'s <see cref="IWebModule.BaseRoute">BaseRoute</see> property.
/// For example, given the following code:</para>
/// <para><code>new ModuleGroup("/download")
///     .WithStaticFilesAt("/docs", "/var/my/documents");</code></para>
/// <para>files contained in the <c>/var/my/documents</c> folder will be
/// available to clients under the <c>/download/docs/</c> URL.</para>
/// </summary>
/// <seealso cref="WebModuleBase" />
/// <seealso cref="IDisposable" />
/// <seealso cref="IWebModuleContainer" />
/// <remarks>
/// Initializes a new instance of the <see cref="ModuleGroup" /> class.
/// </remarks>
/// <param name="baseRoute">The base route served by this module.</param>
/// <param name="isFinalHandler">The value to set the <see cref="IWebModule.IsFinalHandler" /> property to.
/// See the help for the property for more information.</param>
/// <seealso cref="IWebModule.BaseRoute" />
/// <seealso cref="IWebModule.IsFinalHandler" />
public class ModuleGroup(string baseRoute, bool isFinalHandler) : WebModuleBase(baseRoute), IWebModuleContainer, IMimeTypeCustomizer
{
    private readonly WebModuleCollection _modules = new WebModuleCollection(nameof(ModuleGroup));
    private readonly MimeTypeCustomizer _mimeTypeCustomizer = new();

    /// <summary>
    /// Finalizes an instance of the <see cref="ModuleGroup"/> class.
    /// </summary>
    ~ModuleGroup()
    {
        Dispose(false);
    }

    /// <inheritdoc />
    public override sealed bool IsFinalHandler { get; } = isFinalHandler;

    /// <inheritdoc />
    public IComponentCollection<IWebModule> Modules => _modules;

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    string IMimeTypeProvider.GetMimeType(string extension)
        => _mimeTypeCustomizer.GetMimeType(extension);

    bool IMimeTypeProvider.TryDetermineCompression(string mimeType, out bool preferCompression)
        => _mimeTypeCustomizer.TryDetermineCompression(mimeType, out preferCompression);

    /// <inheritdoc />
    public void AddCustomMimeType(string extension, string mimeType)
        => _mimeTypeCustomizer.AddCustomMimeType(extension, mimeType);

    /// <inheritdoc />
    public void PreferCompression(string mimeType, bool preferCompression)
        => _mimeTypeCustomizer.PreferCompression(mimeType, preferCompression);

    /// <inheritdoc />
    protected override Task OnRequestAsync(IHttpContext context)
        => _modules.DispatchRequestAsync(context);

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _modules.Dispose();
    }

    /// <inheritdoc />
    protected override void OnBeforeLockConfiguration()
    {
        base.OnBeforeLockConfiguration();

        _mimeTypeCustomizer.Lock();
    }

    /// <inheritdoc />
    protected override void OnStart(CancellationToken cancellationToken)
        => _modules.StartAll(cancellationToken);
}
