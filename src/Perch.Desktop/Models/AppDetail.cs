using System.Collections.Immutable;

using Perch.Core.Catalog;
using Perch.Core.Modules;

namespace Perch.Desktop.Models;

public sealed record AppDetail(
    AppCardModel Card,
    AppModule? OwningModule,
    AppManifest? Manifest,
    string? ManifestYaml,
    string? ManifestPath,
    ImmutableArray<CatalogEntry> Alternatives,
    ImmutableArray<DotfileFileStatus> FileStatuses = default);

public sealed record DotfileFileStatus(
    string FileName,
    string FullPath,
    bool Exists,
    bool IsSymlink,
    CardStatus Status);
