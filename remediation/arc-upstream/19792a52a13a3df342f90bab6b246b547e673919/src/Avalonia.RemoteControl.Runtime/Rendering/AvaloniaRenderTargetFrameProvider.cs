using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.RemoteControl.Server.Threading;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Rendering;

/// <summary>
/// Captures remote UI frames using Avalonia's render target bitmap API.
/// </summary>
public sealed class AvaloniaRenderTargetFrameProvider : IRemoteControlFrameProvider
{
    private readonly AvaloniaRemoteControlOptions options;
    private readonly IRemoteControlDispatcher dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaRenderTargetFrameProvider"/> class.
    /// </summary>
    /// <param name="options">Remote-control options.</param>
    /// <param name="dispatcher">Avalonia dispatcher.</param>
    public AvaloniaRenderTargetFrameProvider(
        IOptions<AvaloniaRemoteControlOptions> options,
        IRemoteControlDispatcher dispatcher)
    {
        this.options = options.Value;
        this.dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public ValueTask<RemoteControlFrame> CaptureFrameAsync(
        Control root,
        ulong sequence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);

        return dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var renderRoot = RemoteControlRootNormalizer.Normalize(root);
            var rootWidth = renderRoot.Bounds.Width;
            var rootHeight = renderRoot.Bounds.Height;

            if (rootWidth <= 0 || rootHeight <= 0)
            {
                throw new InvalidOperationException("Remote root has no renderable size.");
            }

            const double renderScale = 1;
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(rootWidth * renderScale));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(rootHeight * renderScale));

            if ((long)pixelWidth * pixelHeight > options.MaxFramePixelCount)
            {
                throw new InvalidOperationException("Remote frame exceeds the configured maximum pixel count.");
            }

            using var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight));
            bitmap.Render(renderRoot);

            using var stream = new MemoryStream();
            bitmap.Save(stream);

            return new RemoteControlFrame(
                sequence,
                stream.ToArray(),
                pixelWidth,
                pixelHeight,
                rootWidth,
                rootHeight,
                renderScale,
                DateTimeOffset.UtcNow);
        });
    }
}
