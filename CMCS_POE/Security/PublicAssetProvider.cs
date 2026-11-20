using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace CMCS_POE.Security;

public sealed class PublicAssetProvider : IFileProvider
{
    private readonly IFileProvider _inner;
    private readonly string _uploads;
    public PublicAssetProvider(IFileProvider inner, string webRoot)
    {
        _inner = inner;
        _uploads = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
    }
    private bool IsPrivate(string? physicalPath)
    {
        if (physicalPath is null) return false;
        var path = Path.GetFullPath(physicalPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.Equals(_uploads, comparison) || path.StartsWith(_uploads + Path.DirectorySeparatorChar, comparison);
    }
    public IFileInfo GetFileInfo(string subpath)
    {
        var file = _inner.GetFileInfo(subpath);
        return IsPrivate(file.PhysicalPath) ? new NotFoundFileInfo(subpath) : file;
    }
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var file = _inner.GetFileInfo(subpath);
        return IsPrivate(file.PhysicalPath) ? NotFoundDirectoryContents.Singleton : _inner.GetDirectoryContents(subpath);
    }
    public IChangeToken Watch(string filter) => _inner.Watch(filter);
}
