#if DESKTOP_TESTS
using Perch.Core.Scanner;
using Perch.Desktop.Models;

namespace Perch.Core.Tests.Desktop;

[TestFixture]
public sealed class CertificateCardModelTests
{
    [TestCase("CN=Microsoft Root Authority, OU=Microsoft Corporation, OU=Copyright (c) 1997 Microsoft Corp.", "Microsoft Root Authority")]
    [TestCase("CN=DigiCert Global Root G2, OU=www.digicert.com, O=DigiCert Inc, C=US", "DigiCert Global Root G2")]
    [TestCase("O=Some Org, C=US", "O=Some Org, C=US")]
    [TestCase("CN=Simple", "Simple")]
    [TestCase("cn=lowercase", "lowercase")]
    public void ExtractCn_ParsesDistinguishedName(string dn, string expected)
    {
        var result = CertificateCardModel.ExtractCn(dn);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ExpiryStatus_Expired_WhenPastNotAfter()
    {
        var cert = MakeCert(
            notBefore: new DateTime(2020, 1, 1),
            notAfter: new DateTime(2024, 1, 1));
        var now = new DateTime(2025, 6, 1);

        var model = new CertificateCardModel(cert, now);

        Assert.That(model.ExpiryStatus, Is.EqualTo(CertificateExpiryStatus.Expired));
    }

    [Test]
    public void ExpiryStatus_ExpiringSoon_WhenWithin30Days()
    {
        var cert = MakeCert(
            notBefore: new DateTime(2020, 1, 1),
            notAfter: new DateTime(2025, 7, 1));
        var now = new DateTime(2025, 6, 15);

        var model = new CertificateCardModel(cert, now);

        Assert.That(model.ExpiryStatus, Is.EqualTo(CertificateExpiryStatus.ExpiringSoon));
    }

    [Test]
    public void ExpiryStatus_Valid_WhenWellBeforeExpiry()
    {
        var cert = MakeCert(
            notBefore: new DateTime(2020, 1, 1),
            notAfter: new DateTime(2030, 1, 1));
        var now = new DateTime(2025, 6, 1);

        var model = new CertificateCardModel(cert, now);

        Assert.That(model.ExpiryStatus, Is.EqualTo(CertificateExpiryStatus.Valid));
    }

    [TestCase("ABC123", true)]
    [TestCase("abc123", true)]
    [TestCase("Microsoft", true)]
    [TestCase("DigiCert", true)]
    [TestCase("nonexistent-xyz", false)]
    public void MatchesSearch_MatchesThumbprintSubjectIssuer(string query, bool expected)
    {
        var cert = new DetectedCertificate(
            Thumbprint: "ABC123DEF456",
            Subject: "CN=Microsoft Root Authority",
            Issuer: "CN=DigiCert Global Root",
            FriendlyName: null,
            NotBefore: DateTime.MinValue,
            NotAfter: DateTime.MaxValue,
            HasPrivateKey: false,
            Store: CertificateStoreName.TrustedRoot);

        var model = new CertificateCardModel(cert);

        Assert.That(model.MatchesSearch(query), Is.EqualTo(expected));
    }

    [Test]
    public void MatchesSearch_MatchesFriendlyName()
    {
        var cert = MakeCert(friendlyName: "My Test Certificate");
        var model = new CertificateCardModel(cert);

        Assert.That(model.MatchesSearch("Test Cert"), Is.True);
    }

    [Test]
    public void MatchesSearch_EmptyQuery_ReturnsTrue()
    {
        var cert = MakeCert();
        var model = new CertificateCardModel(cert);

        Assert.That(model.MatchesSearch(""), Is.True);
        Assert.That(model.MatchesSearch("  "), Is.True);
    }

    private static DetectedCertificate MakeCert(
        string thumbprint = "AABB",
        string subject = "CN=Test",
        string issuer = "CN=Issuer",
        string? friendlyName = null,
        DateTime? notBefore = null,
        DateTime? notAfter = null,
        bool hasPrivateKey = false,
        CertificateStoreName store = CertificateStoreName.TrustedRoot)
    {
        return new DetectedCertificate(
            thumbprint,
            subject,
            issuer,
            friendlyName,
            notBefore ?? DateTime.MinValue,
            notAfter ?? DateTime.MaxValue,
            hasPrivateKey,
            store);
    }
}
#endif
