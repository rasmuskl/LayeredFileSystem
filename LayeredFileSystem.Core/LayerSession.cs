using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace LayeredFileSystem.Core;

public class LayerSession(
    IFileSystem fileSystem,
    string workingDirectory,
    ChangeDetector changeDetector,
    ILayerCache layerCache,
    ITarLayerReader tarReader,
    ITarLayerWriter tarWriter,
    IPathNormalizer pathNormalizer)
    : ILayerSession
{
    private readonly List<LayerInfo> _appliedLayers = new();
    private bool _disposed;

    public string WorkingDirectory => workingDirectory;
    public IReadOnlyList<LayerInfo> AppliedLayers => _appliedLayers.AsReadOnly();

    public async Task<ILayerContext> BeginLayerAsync(string inputHash, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LayerSession));

        if (string.IsNullOrWhiteSpace(inputHash))
            throw new ArgumentException("Input hash cannot be null or empty", nameof(inputHash));

        return new LayerContext(
            fileSystem,
            workingDirectory,
            inputHash, // Use input hash as the identifier
            false, // Context will handle cache logic
            changeDetector,
            layerCache,
            tarWriter,
            pathNormalizer,
            this);
    }

    internal void AddAppliedLayer(LayerInfo layerInfo)
    {
        _appliedLayers.Add(layerInfo);
    }

    private async Task<string> CalculateLayerHashAsync(string inputHash)
    {
        // Combine input hash with the current state of applied layers
        var stateBuilder = new StringBuilder();
        stateBuilder.Append(inputHash);
        
        foreach (var layer in _appliedLayers)
        {
            stateBuilder.Append(layer.Hash);
        }

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(stateBuilder.ToString()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}