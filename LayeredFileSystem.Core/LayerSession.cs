using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace LayeredFileSystem.Core;

public class LayerSession : ILayerSession
{
    private readonly IFileSystem _fileSystem;
    private readonly string _workingDirectory;
    private readonly ChangeDetector _changeDetector;
    private readonly ILayerCache _layerCache;
    private readonly ITarLayerReader _tarReader;
    private readonly ITarLayerWriter _tarWriter;
    private readonly IPathNormalizer _pathNormalizer;
    private readonly List<LayerInfo> _appliedLayers;
    private bool _disposed;

    public LayerSession(
        IFileSystem fileSystem,
        string workingDirectory,
        ChangeDetector changeDetector,
        ILayerCache layerCache,
        ITarLayerReader tarReader,
        ITarLayerWriter tarWriter,
        IPathNormalizer pathNormalizer)
    {
        _fileSystem = fileSystem;
        _workingDirectory = workingDirectory;
        _changeDetector = changeDetector;
        _layerCache = layerCache;
        _tarReader = tarReader;
        _tarWriter = tarWriter;
        _pathNormalizer = pathNormalizer;
        _appliedLayers = new List<LayerInfo>();
    }

    public string WorkingDirectory => _workingDirectory;
    public IReadOnlyList<LayerInfo> AppliedLayers => _appliedLayers.AsReadOnly();

    public async Task<ILayerContext> BeginLayerAsync(string inputHash, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LayerSession));

        if (string.IsNullOrWhiteSpace(inputHash))
            throw new ArgumentException("Input hash cannot be null or empty", nameof(inputHash));

        return new LayerContext(
            _fileSystem,
            _workingDirectory,
            inputHash, // Use input hash as the identifier
            false, // Context will handle cache logic
            _changeDetector,
            _layerCache,
            _tarWriter,
            _pathNormalizer,
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