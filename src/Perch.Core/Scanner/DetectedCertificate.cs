namespace Perch.Core.Scanner;

public enum CertificateStoreName
{
    Personal,
    TrustedRoot,
    Intermediate,
    TrustedPeople,
}

public sealed record DetectedCertificate(
    string Thumbprint,
    string Subject,
    string Issuer,
    string? FriendlyName,
    DateTime NotBefore,
    DateTime NotAfter,
    bool HasPrivateKey,
    CertificateStoreName Store);
