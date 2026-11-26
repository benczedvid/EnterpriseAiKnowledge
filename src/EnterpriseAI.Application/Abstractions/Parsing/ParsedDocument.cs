namespace EnterpriseAI.Application.Abstractions.Parsing;

public sealed record ParsedDocument(
    string Title,
    IReadOnlyList<ParsedSection> Sections)
{
    public int CharacterCount =>
        Sections.Sum(section => section.Content.Length);

    public bool HasContent =>
        Sections.Any(section =>
            !string.IsNullOrWhiteSpace(section.Content));
}