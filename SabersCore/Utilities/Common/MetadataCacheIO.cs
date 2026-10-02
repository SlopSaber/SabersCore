using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using SabersCore.Models;
using SabersCore.Utilities.Extensions;

namespace SabersCore.Utilities.Common;

internal static class MetadataCacheIO
{
    internal record ReadResult(CacheFileModel? Metadata, byte[]?[] Images, Exception?[] ImageErrors);
    internal record PreparedMetadata(SaberMetadataModel Metadata, int FileIndex,
        RichTextString SaberName, RichTextString AuthorName);

    internal static int[] SelectUncachedFiles(object state)
    {
        var (metadata, hashes, token) = ((SaberMetadataModel[], string[], CancellationToken))state;
        var cachedHashes = new HashSet<string>();
        foreach (var meta in metadata)
        {
            token.ThrowIfCancellationRequested();
            cachedHashes.Add(meta.Hash);
        }
        return hashes.Select((hash, index) => (hash, index))
            .Where(file =>
            {
                token.ThrowIfCancellationRequested();
                return !cachedHashes.Contains(file.hash);
            }).Select(file => file.index).ToArray();
    }

    internal static PreparedMetadata[] PrepareMetadata(object state)
    {
        var (metadata, hashes, token) = ((SaberMetadataModel[], string[], CancellationToken))state;
        return metadata.Join(hashes.Select((hash, index) =>
            {
                token.ThrowIfCancellationRequested();
                return (hash, index);
            }),
            meta =>
            {
                token.ThrowIfCancellationRequested();
                return meta.Hash;
            }, file => file.hash,
            (meta, file) =>
            {
                token.ThrowIfCancellationRequested();
                return new PreparedMetadata(meta, file.index,
                    RichTextString.Create(meta.SaberName), RichTextString.Create(meta.AuthorName));
            }).ToArray();
    }

    internal static ReadResult Read(object state)
    {
        var (path, token) = ((string, CancellationToken))state;
        token.ThrowIfCancellationRequested();
        if (!File.Exists(path)) return new(null, [], []);
        using var archive = ZipFile.OpenRead(path);
        using var metadataStream = archive.GetEntry("metadata.json")?.Open();
        if (metadataStream == null) return new(null, [], []);
        var metadata = metadataStream.DeserializeStream<CacheFileModel>()?.WithValidation();
        if (metadata == null) return new(null, [], []);
        byte[]?[] images = new byte[metadata.CachedMetadata.Length][];
        Exception?[] errors = new Exception[images.Length];
        for (var i = 0; i < images.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var entry = archive.GetEntry($"images/{metadata.CachedMetadata[i].Hash}.png");
                if (entry != null) images[i] = ReadEntry(entry, token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error)
            {
                errors[i] = error;
                break;
            }
        }
        return new(metadata, images, errors);
    }

    internal static byte[] ReadEntry(ZipArchiveEntry entry, CancellationToken token)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var count = stream.Read(buffer, 0, buffer.Length);
            if (count == 0) break;
            memory.Write(buffer, 0, count);
        }
        token.ThrowIfCancellationRequested();
        return memory.ToArray();
    }

    internal static void CreateTemporaryDirectories(object state)
    {
        var directory = Directory.CreateDirectory((string)state);
        directory.CreateSubdirectory("images");
    }

    internal static void WriteImage(object state)
    {
        var (path, image) = ((string, byte[]))state;
        File.WriteAllBytes(path, image);
    }

    internal static void SaveArchive(object state)
    {
        var (temporaryPath, archivePath, metadata, json) = ((string, string, CacheFileModel?, string?))state;
        json ??= Serialize(metadata!);
        File.WriteAllText(Path.Combine(temporaryPath, "metadata.json"), json);
        if (File.Exists(archivePath)) File.Delete(archivePath);
        ZipFile.CreateFromDirectory(temporaryPath, archivePath);
    }

    internal static void DeleteTemporaryDirectory(object state)
        => Directory.Delete((string)state, true);

    internal static void DeleteTemporaryDirectoryIfPresent(object state)
    {
        var path = (string)state;
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private static string Serialize(CacheFileModel metadata)
    {
        var serializer = new JsonSerializer();
        using var text = new StringWriter(new StringBuilder(256), CultureInfo.InvariantCulture);
        using var writer = new JsonTextWriter(text) { Formatting = Formatting.None };
        serializer.Serialize(writer, metadata, null);
        return text.ToString();
    }
}
