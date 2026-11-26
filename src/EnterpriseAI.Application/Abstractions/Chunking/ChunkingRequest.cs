using EnterpriseAI.Application.Abstractions.Parsing;

namespace EnterpriseAI.Application.Abstractions.Chunking;

public sealed record ChunkingRequest(
    ParsedDocument Document);