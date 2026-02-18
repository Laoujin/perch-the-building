using Perch.Core.Scanner;

namespace Perch.Core.Tests.Scanner;

[TestFixture]
public sealed class CertificateScannerTests
{
    [Test]
    public async Task ScanAsync_ReturnsCertificates_FromTrustedRootStore()
    {
        var scanner = new CertificateScanner();

        var result = await scanner.ScanAsync();

        var rootCerts = result.Where(c => c.Store == CertificateStoreName.TrustedRoot).ToList();
        Assert.That(rootCerts, Is.Not.Empty);
    }

    [Test]
    public async Task ScanAsync_CertificatesHaveRequiredFields()
    {
        var scanner = new CertificateScanner();

        var result = await scanner.ScanAsync();

        Assert.That(result, Is.Not.Empty);
        foreach (var cert in result.Take(5))
        {
            Assert.That(cert.Thumbprint, Is.Not.Null.And.Not.Empty);
            Assert.That(cert.Subject, Is.Not.Null.And.Not.Empty);
            Assert.That(cert.Issuer, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void ScanAsync_SupportsCancellation()
    {
        var scanner = new CertificateScanner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            () => scanner.ScanAsync(cts.Token));
    }
}
