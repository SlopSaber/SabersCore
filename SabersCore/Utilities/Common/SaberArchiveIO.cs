using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using SabersCore.Models;

namespace SabersCore.Utilities.Common;

internal static class SaberArchiveIO
{
    internal sealed class ReadResult
    {
        internal bool FileExists;
        internal SaberLoaderError Error = SaberLoaderError.FileNotFound;
        internal Saber2Model? Model;
        internal byte[]? Bundle;
        internal byte[]? Icon;
        internal Exception? ReadError;
        internal Exception? IconError;
    }

    internal static ReadResult Read(object state)
    {
        var result = new ReadResult();
        var (path, token) = ((string, CancellationToken))state;
        try
        {
            token.ThrowIfCancellationRequested();
            var file = new FileInfo(path);
            result.FileExists = file.Exists;
            if (!result.FileExists) return result;
            using var fileStream = file.OpenRead();
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var jsonEntry = archive.GetEntry("metadata.json");
            if (jsonEntry == null) return result;
            using var jsonStream = jsonEntry.Open();
            var model = jsonStream.DeserializeStream<Saber2Model>();
            if (model == null || !model.Assets.TryGetValue(AssetPlatform.PC, out var asset)) return result;
            var bundleEntry = archive.GetEntry(asset.FilePath);
            if (bundleEntry == null) return result;
            result.Model = model;
            result.Bundle = MetadataCacheIO.ReadEntry(bundleEntry, token);
            result.Error = SaberLoaderError.None;
            try
            {
                if (!string.IsNullOrEmpty(model.IconPath))
                {
                    var iconEntry = archive.GetEntry(model.IconPath);
                    if (iconEntry != null) result.Icon = MetadataCacheIO.ReadEntry(iconEntry, token);
                }
            }
            catch (Exception error)
            {
                result.IconError = error;
            }
        }
        catch (Exception error)
        {
            result.ReadError = error;
        }
        return result;
    }
}
