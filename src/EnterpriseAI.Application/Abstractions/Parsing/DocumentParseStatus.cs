namespace EnterpriseAI.Application.Abstractions.Parsing;

public enum DocumentParseStatus
{
    Success = 0,
    RequiresOcr = 1,
    Unsupported = 2
}