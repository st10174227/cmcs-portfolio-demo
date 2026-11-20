using System.IO.Compression;
using System.Security.Cryptography;
using CMCS_POE.Models;
using CMCS_Part2.Helpers;
using Microsoft.AspNetCore.DataProtection;

namespace CMCS_POE.Services;

public sealed record DownloadDocument(byte[] Bytes, string ContentType, string Name);

public sealed class ClaimDocuments
{
    public const long MaximumLength = 5 * 1024 * 1024;
    private readonly long _maximumWorkspaceBytes;
    private readonly string _root;
    private readonly string _legacyRoot;
    private readonly IDataProtector _protector;
    private readonly ILogger<ClaimDocuments> _logger;

    public ClaimDocuments(string dataDirectory, string contentRoot, IDataProtectionProvider protection,
        ILogger<ClaimDocuments> logger, long maximumWorkspaceBytes = long.MaxValue)
    {
        _root = Path.Combine(dataDirectory, "Attachments");
        _legacyRoot = Path.GetFullPath(Path.Combine(contentRoot, "wwwroot", "uploads"));
        _protector = protection.CreateProtector("cmcs-personal.claim-document.v1");
        _logger = logger;
        _maximumWorkspaceBytes = maximumWorkspaceBytes;
        Directory.CreateDirectory(_root);
    }

    public (string Path, string Name) Save(IFormFile upload)
    {
        var name = Path.GetFileName(upload.FileName.Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c)).Take(180).ToArray());
        var extension = System.IO.Path.GetExtension(name).ToLowerInvariant();
        if (upload.Length <= 0 || upload.Length > MaximumLength ||
            !new[] { ".pdf", ".docx", ".xlsx" }.Contains(extension))
            throw new InvalidDataException("Choose a PDF, DOCX or XLSX document no larger than 5 MB.");

        using var input = upload.OpenReadStream();
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memory.Length + count > MaximumLength)
                throw new InvalidDataException("The document exceeds the 5 MB limit.");
            memory.Write(buffer, 0, count);
        }
        var bytes = memory.ToArray();
        ValidateFormat(bytes, extension);
        var id = Guid.NewGuid().ToString("N");
        var protectedBytes = _protector.CreateProtector(id).Protect(bytes);
        if (Directory.EnumerateFiles(_root, "*.bin").Sum(path => new FileInfo(path).Length) + protectedBytes.Length > _maximumWorkspaceBytes)
            throw new InvalidDataException("Your demo attachment storage is full. Reset the demo to load a fresh workspace.");
        using (var output = new FileStream(DiskPath(id), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            output.Write(protectedBytes);
        return ("personal/" + id, name);
    }

    public DownloadDocument? Read(Claim claim)
    {
        if (claim.DocumentPath is null) return null;
        byte[] bytes;
        var id = PersonalId(claim.DocumentPath);
        if (id is not null)
        {
            var path = DiskPath(id);
            if (!File.Exists(path)) return null;
            bytes = _protector.CreateProtector(id).Unprotect(File.ReadAllBytes(path));
        }
        else
        {
            // Compatibility is confined to a saved claim's old personal upload path.
            const string prefix = "/uploads/";
            if (!claim.DocumentPath.StartsWith(prefix, StringComparison.Ordinal)) return null;
            var relative = claim.DocumentPath[prefix.Length..];
            if (relative.Length == 0 || relative.Contains('/') || relative.Contains('\\') ||
                relative.Contains(':') || relative is "." or "..") return null;
            var path = Path.Combine(_legacyRoot, relative);
            if (!File.Exists(path) || new FileInfo(path).Length > MaximumLength + 1024) return null;
            var temporary = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                FileEncryptionHelper.DecryptFile(path, temporary);
                bytes = File.ReadAllBytes(temporary);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        var name = Path.GetFileName((claim.DocumentName ?? "document").Replace('\\', '/'));
        var type = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
        return new DownloadDocument(bytes, type, name);
    }

    public void Remove(string? storedPath)
    {
        var id = PersonalId(storedPath);
        if (id is null) return; // Legacy files are retained; no user-supplied paths are deleted.
        try { File.Delete(DiskPath(id)); }
        catch (IOException exception) { _logger.LogWarning(exception, "An unreferenced private document needs cleanup."); }
        catch (UnauthorizedAccessException exception) { _logger.LogWarning(exception, "Private document cleanup was denied."); }
    }

    private string DiskPath(string id) => Path.Combine(_root, id + ".bin");
    private static string? PersonalId(string? path) => path is not null &&
        path.StartsWith("personal/", StringComparison.Ordinal) && Guid.TryParseExact(path[9..], "N", out var id)
            ? id.ToString("N") : null;

    private static void ValidateFormat(byte[] bytes, string extension)
    {
        if (extension == ".pdf")
        {
            if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
                throw new InvalidDataException("The selected file is not a PDF document.");
            return;
        }
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var entry = extension == ".docx" ? "word/document.xml" : "xl/workbook.xml";
            if (archive.Entries.Count > 5000 || archive.Entries.Sum(e => e.Length) > 50 * 1024 * 1024 ||
                archive.GetEntry("[Content_Types].xml") is null || archive.GetEntry(entry) is null)
                throw new InvalidDataException("The document does not match its selected file type.");
        }
        catch (InvalidDataException) { throw new InvalidDataException("Choose a valid DOCX or XLSX document."); }
    }
}
