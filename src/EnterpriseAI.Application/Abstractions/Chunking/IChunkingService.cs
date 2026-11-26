namespace EnterpriseAI.Application.Abstractions.Chunking;

public interface IChunkingService
{
    ChunkingResult CreateChunks(ChunkingRequest request);
}