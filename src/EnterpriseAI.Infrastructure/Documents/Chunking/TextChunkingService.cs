using System.Text;
using EnterpriseAI.Application.Abstractions.Chunking;
using EnterpriseAI.Application.Abstractions.Parsing;
using Microsoft.Extensions.Options;

namespace EnterpriseAI.Infrastructure.Documents.Chunking;

public sealed class TextChunkingService : IChunkingService
{
    private readonly TextChunkingOptions _options;

    public TextChunkingService(IOptions<TextChunkingOptions> options)
    {
        _options = options.Value;
    }

    public ChunkingResult CreateChunks(ChunkingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);

        if (!request.Document.HasContent)
        {
            throw new ArgumentException("Parsed document must contain text.", nameof(request));
        }

        var chunks = new List<TextChunk>();

        foreach (var section in request.Document.Sections.OrderBy(section => section.Order))
        {
            AddSectionChunks(section, chunks);
        }

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("Chunking produced no text chunks.");
        }

        return new ChunkingResult(chunks);
    }

    private void AddSectionChunks(ParsedSection section, ICollection<TextChunk> chunks)
    {
        var content = section.Content.Trim();

        if (content.Length <= _options.MaximumChunkSize)
        {
            AddChunk(
                chunks,
                content,
                section.Heading,
                section.PageNumber);

            return;
        }

        var startIndex = 0;

        while (startIndex < content.Length)
        {
            var remainingLength = content.Length - startIndex;

            if (remainingLength <= _options.MaximumChunkSize)
            {
                var remainder = content[startIndex..].Trim();

                if (remainder.Length > 0)
                {
                    AddOrMergeRemainder(
                        chunks,
                        remainder,
                        section);
                }

                break;
            }

            var desiredEnd = Math.Min(
                startIndex + _options.TargetChunkSize,
                content.Length);

            var maximumEnd = Math.Min(
                startIndex + _options.MaximumChunkSize,
                content.Length);

            var splitIndex = FindSplitIndex(
                content,
                startIndex,
                desiredEnd,
                maximumEnd);

            var chunkContent = content[startIndex..splitIndex].Trim();

            if (chunkContent.Length > 0)
            {
                AddChunk(
                    chunks,
                    chunkContent,
                    section.Heading,
                    section.PageNumber);
            }

            var nextStart = Math.Max(
                splitIndex - _options.OverlapSize,
                startIndex + 1);

            nextStart = MoveToWordBoundary(content, nextStart);

            if (nextStart <= startIndex)
            {
                nextStart = splitIndex;
            }

            startIndex = nextStart;
        }
    }

    private int FindSplitIndex(
        string content,
        int startIndex,
        int desiredEnd,
        int maximumEnd)
    {
        var paragraphBreak = FindLastBoundary(
            content,
            startIndex,
            maximumEnd,
            "\n\n");

        if (paragraphBreak >= desiredEnd)
        {
            return paragraphBreak;
        }

        var sentenceEnd = FindLastSentenceEnd(
            content,
            startIndex,
            maximumEnd);

        if (sentenceEnd >= desiredEnd)
        {
            return sentenceEnd;
        }

        var whitespace = FindLastWhitespace(
            content,
            desiredEnd,
            maximumEnd);

        return whitespace > startIndex ? whitespace : maximumEnd;
    }

    private static int FindLastBoundary(
        string content,
        int startIndex,
        int maximumEnd,
        string boundary)
    {
        var index = content.LastIndexOf(
            boundary,
            maximumEnd - 1,
            maximumEnd - startIndex,
            StringComparison.Ordinal);

        return index < startIndex ? -1 : index + boundary.Length;
    }

    private static int FindLastSentenceEnd(
        string content,
        int startIndex,
        int maximumEnd)
    {
        for (var index = maximumEnd - 1; index >= startIndex; index--)
        {
            if (content[index] is '.' or '!' or '?')
            {
                return index + 1;
            }
        }

        return -1;
    }

    private static int FindLastWhitespace(
        string content,
        int desiredEnd,
        int maximumEnd)
    {
        for (var index = maximumEnd - 1; index >= desiredEnd;  index--)
        {
            if (char.IsWhiteSpace(content[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static int MoveToWordBoundary(string content, int startIndex)
    {
        while (startIndex < content.Length && !char.IsWhiteSpace(content[startIndex]))
        {
            startIndex++;
        }

        while (startIndex < content.Length && char.IsWhiteSpace(content[startIndex]))
        {
            startIndex++;
        }

        return startIndex;
    }

    private void AddOrMergeRemainder(
        ICollection<TextChunk> chunks,
        string remainder,
        ParsedSection section)
    {
        if (remainder.Length >= _options.MinimumChunkSize || chunks.Count == 0)
        {
            AddChunk(
                chunks,
                remainder,
                section.Heading,
                section.PageNumber);

            return;
        }

        var previous = chunks.Last();

        if (previous.SectionTitle == section.Heading
            && previous.PageNumber == section.PageNumber
            && previous.Content.Length
                + Environment.NewLine.Length
                + remainder.Length
                <= _options.MaximumChunkSize)
        {
            chunks.Remove(previous);

            AddChunk(
                chunks,
                $"{previous.Content}{Environment.NewLine}{remainder}",
                section.Heading,
                section.PageNumber);

            return;
        }

        AddChunk(
            chunks,
            remainder,
            section.Heading,
            section.PageNumber);
    }

    private void AddChunk(
        ICollection<TextChunk> chunks,
        string content,
        string? sectionTitle,
        int? pageNumber)
    {
        var normalizedContent = content.Trim();

        if (normalizedContent.Length == 0)
        {
            return;
        }

        var estimatedTokenCount = Math.Max(
            1,
            (int)Math.Ceiling(
                normalizedContent.Length /
                (double)_options.EstimatedCharactersPerToken));

        chunks.Add(
            new TextChunk(
                Content: normalizedContent,
                SectionTitle: sectionTitle,
                PageNumber: pageNumber,
                ChunkIndex: chunks.Count,
                EstimatedTokenCount: estimatedTokenCount));
    }
}