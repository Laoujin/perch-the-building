using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Perch.Core.Scanner;

namespace Perch.Desktop.Models;

public partial class CertificateStoreGroupModel : ObservableObject
{
    public CertificateStoreName Store { get; }
    public string DisplayName { get; }
    public ObservableCollection<CertificateCardModel> Certificates { get; }

    [ObservableProperty]
    private bool _isExpanded = true;

    public CertificateStoreGroupModel(CertificateStoreName store, IEnumerable<CertificateCardModel> certificates)
    {
        Store = store;
        DisplayName = store switch
        {
            CertificateStoreName.Personal => "Personal",
            CertificateStoreName.TrustedRoot => "Trusted Root CAs",
            CertificateStoreName.Intermediate => "Intermediate CAs",
            CertificateStoreName.TrustedPeople => "Trusted People",
            _ => store.ToString(),
        };
        Certificates = new ObservableCollection<CertificateCardModel>(certificates);
    }

    public bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Certificates.Any(c => c.MatchesSearch(query));
    }
}
