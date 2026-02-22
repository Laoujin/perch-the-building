using System.Collections.Immutable;

using Perch.Core.Git;
using Perch.Core.Modules;

namespace Perch.Core.Catalog;

public sealed class GalleryOverlayService : IGalleryOverlayService
{
    public AppManifest Merge(AppManifest manifest, CatalogEntry gallery)
    {
        var links = MergeLinks(manifest.Links, gallery.Config?.Links ?? ImmutableArray<CatalogConfigLink>.Empty);
        var cleanFilter = manifest.CleanFilter ?? ConvertCleanFilter(gallery.Id, gallery.Config?.CleanFilter);
        var vscodeExtensions = MergeExtensions(manifest.VscodeExtensions, gallery.Extensions);
        string displayName = manifest.DisplayName == manifest.ModuleName && gallery.DisplayName != null
            ? gallery.DisplayName
            : manifest.DisplayName;

        return manifest with
        {
            Links = links,
            CleanFilter = cleanFilter,
            VscodeExtensions = vscodeExtensions,
            DisplayName = displayName,
        };
    }

    private static ImmutableArray<LinkEntry> MergeLinks(
        ImmutableArray<LinkEntry> manifestLinks,
        ImmutableArray<CatalogConfigLink> galleryLinks)
    {
        if (galleryLinks.IsDefaultOrEmpty)
        {
            return manifestLinks;
        }

        var manifestSources = manifestLinks.IsDefaultOrEmpty
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : manifestLinks.Select(l => l.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var merged = new List<LinkEntry>(manifestLinks.IsDefaultOrEmpty ? [] : manifestLinks);

        foreach (var gl in galleryLinks)
        {
            if (manifestSources.Contains(gl.Source))
            {
                continue;
            }

            merged.Add(new LinkEntry(
                gl.Source,
                null,
                gl.Targets,
                gl.LinkType,
                gl.Template));
        }

        return merged.ToImmutableArray();
    }

    private static CleanFilterDefinition? ConvertCleanFilter(string appId, CatalogCleanFilter? filter)
    {
        if (filter == null || filter.Rules.IsDefaultOrEmpty || filter.Files.IsDefaultOrEmpty)
        {
            return null;
        }

        return new CleanFilterDefinition($"{appId}-clean", null, filter.Files, filter.Rules);
    }

    private static ImmutableArray<string> MergeExtensions(
        ImmutableArray<string> manifestExtensions,
        CatalogExtensions? galleryExtensions)
    {
        if (galleryExtensions == null)
        {
            return manifestExtensions;
        }

        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!manifestExtensions.IsDefaultOrEmpty)
        {
            foreach (var ext in manifestExtensions)
            {
                all.Add(ext);
            }
        }

        foreach (var ext in galleryExtensions.Bundled)
        {
            all.Add(ext);
        }

        foreach (var ext in galleryExtensions.Recommended)
        {
            all.Add(ext);
        }

        return all.Count > 0 ? all.ToImmutableArray() : ImmutableArray<string>.Empty;
    }
}
