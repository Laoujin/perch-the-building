using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Scanner;

namespace Perch.Desktop.Models;

public enum CertificateExpiryStatus
{
    Valid,
    ExpiringSoon,
    Expired,
}

public partial class CertificateCardModel : ObservableObject
{
    public DetectedCertificate Certificate { get; }
    public string SubjectDisplayName { get; }
    public string IssuerDisplayName { get; }
    public CertificateExpiryStatus ExpiryStatus { get; }

    [ObservableProperty]
    private bool _isExpanded;

    public CertificateCardModel(DetectedCertificate certificate)
        : this(certificate, DateTime.Now)
    {
    }

    internal CertificateCardModel(DetectedCertificate certificate, DateTime now)
    {
        Certificate = certificate;
        SubjectDisplayName = ExtractCn(certificate.Subject);
        IssuerDisplayName = ExtractCn(certificate.Issuer);
        ExpiryStatus = ComputeExpiryStatus(certificate, now);
    }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Certificate.Thumbprint.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Certificate.Subject.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Certificate.Issuer.Contains(query, StringComparison.OrdinalIgnoreCase)
            || SubjectDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (Certificate.FriendlyName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    internal static string ExtractCn(string distinguishedName)
    {
        const string prefix = "CN=";
        var idx = distinguishedName.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return distinguishedName;

        var start = idx + prefix.Length;
        var end = distinguishedName.IndexOf(',', start);
        return end < 0 ? distinguishedName[start..] : distinguishedName[start..end];
    }

    private static CertificateExpiryStatus ComputeExpiryStatus(DetectedCertificate cert, DateTime now)
    {
        if (now > cert.NotAfter)
            return CertificateExpiryStatus.Expired;

        if ((cert.NotAfter - now).TotalDays < 30)
            return CertificateExpiryStatus.ExpiringSoon;

        return CertificateExpiryStatus.Valid;
    }
}
