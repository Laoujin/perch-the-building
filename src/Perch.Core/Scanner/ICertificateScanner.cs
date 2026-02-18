using System.Collections.Immutable;

namespace Perch.Core.Scanner;

public interface ICertificateScanner
{
    Task<ImmutableArray<DetectedCertificate>> ScanAsync(CancellationToken cancellationToken = default);
}
