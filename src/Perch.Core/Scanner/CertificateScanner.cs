using System.Collections.Immutable;
using System.Security.Cryptography.X509Certificates;

namespace Perch.Core.Scanner;

public sealed class CertificateScanner : ICertificateScanner
{
    private static readonly (StoreName Name, CertificateStoreName Mapped)[] Stores =
    [
        (StoreName.My, CertificateStoreName.Personal),
        (StoreName.Root, CertificateStoreName.TrustedRoot),
        (StoreName.CertificateAuthority, CertificateStoreName.Intermediate),
        (StoreName.TrustedPeople, CertificateStoreName.TrustedPeople),
    ];

    public Task<ImmutableArray<DetectedCertificate>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectedCertificate>();

        foreach (var (storeName, mapped) in Stores)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanStore(storeName, mapped, results);
        }

        return Task.FromResult(results.ToImmutableArray());
    }

    public Task RemoveAsync(DetectedCertificate certificate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var storeName = certificate.Store switch
        {
            CertificateStoreName.Personal => StoreName.My,
            CertificateStoreName.TrustedRoot => StoreName.Root,
            CertificateStoreName.Intermediate => StoreName.CertificateAuthority,
            CertificateStoreName.TrustedPeople => StoreName.TrustedPeople,
            _ => throw new ArgumentOutOfRangeException(nameof(certificate), $"Unknown store: {certificate.Store}"),
        };

        using var store = new X509Store(storeName, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
        foreach (var match in matches)
        {
            store.Remove(match);
            match.Dispose();
        }

        return Task.CompletedTask;
    }

    private static void ScanStore(StoreName storeName, CertificateStoreName mapped, List<DetectedCertificate> results)
    {
        try
        {
            using var store = new X509Store(storeName, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            foreach (var cert in store.Certificates)
            {
                try
                {
                    results.Add(new DetectedCertificate(
                        cert.Thumbprint,
                        cert.Subject,
                        cert.Issuer,
                        string.IsNullOrWhiteSpace(cert.FriendlyName) ? null : cert.FriendlyName,
                        cert.NotBefore,
                        cert.NotAfter,
                        cert.HasPrivateKey,
                        mapped));
                }
                finally
                {
                    cert.Dispose();
                }
            }
        }
        catch (Exception) when (storeName != StoreName.Root)
        {
            // Non-critical store access failure -- Root should always exist
        }
    }
}
