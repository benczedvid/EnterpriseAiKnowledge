namespace EnterpriseAI.Application.Abstractions.Documents;

/*
 * Az elavult dokumentumváltozatok listája.
 *
 * Szabály: mindig a legfrissebb dokumentumváltozat számít.
 * Az itt felsorolt dokumentumok indexelve maradnak, de a
 * szemantikus keresés és a tudásalapú válaszadás nem
 * használja őket forrásként.
 */
public sealed class DocumentVersioningOptions
{
    public const string SectionName = "DocumentVersioning";

    public List<SupersededDocumentOptions> SupersededDocuments { get; set; } = [];

    public IReadOnlyCollection<string> GetSupersededSourceIds()
    {
        return SupersededDocuments
            .Select(document => document.SourceId.Trim())
            .Where(sourceId => sourceId.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class SupersededDocumentOptions
{
    /*
     * Az elavult dokumentum SharePoint azonosítója.
     */
    public string SourceId { get; set; } = string.Empty;

    /*
     * Az aktuális, érvényes dokumentumváltozat
     * SharePoint azonosítója.
     */
    public string SupersededBySourceId { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
