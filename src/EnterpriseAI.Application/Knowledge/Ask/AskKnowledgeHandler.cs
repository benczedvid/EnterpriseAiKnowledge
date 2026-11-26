using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EnterpriseAI.Application.Abstractions.AI;
using EnterpriseAI.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace EnterpriseAI.Application.Knowledge.Ask;

public sealed partial class AskKnowledgeHandler
{
    private const int MaximumQuestionLength = 2000;
    private const int MaximumRetrievalLimit = 10;
    private const double MinimumSimilarity = 0.60;

    private const string NoAnswerMessage = "A rendelkezésre álló vállalati dokumentumok alapján nem található megbízható válasz a kérdésre.";

    private static readonly JsonSerializerOptions
        AnswerJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

    private readonly IEmbeddingService _embeddingService;
    private readonly ISemanticSearchRepository _semanticSearchRepository;
    private readonly ILlmService _llmService;
    private readonly ILogger<AskKnowledgeHandler> _logger;


    public AskKnowledgeHandler(
        IEmbeddingService embeddingService,
        ISemanticSearchRepository semanticSearchRepository,
        ILlmService llmService,
        ILogger<AskKnowledgeHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(embeddingService);
        ArgumentNullException.ThrowIfNull(semanticSearchRepository);
        ArgumentNullException.ThrowIfNull(llmService);
        ArgumentNullException.ThrowIfNull(logger);

        _embeddingService = embeddingService;
        _semanticSearchRepository = semanticSearchRepository;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<AskKnowledgeResponse> HandleAsync(AskKnowledgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var question = GetQuestion(request.Question);

        ValidateRetrievalLimit(request.RetrievalLimit);

        var embeddingResponse = await _embeddingService.CreateEmbeddingsAsync(
                new EmbeddingRequest([question]),
                cancellationToken);

        if (embeddingResponse.Embeddings.Count != 1)
        {
            throw new InvalidOperationException("The embedding service must return exactly one embedding for the question.");
        }

        var searchResults = await _semanticSearchRepository.SearchAsync(
                embeddingResponse.Embeddings[0],
                request.RetrievalLimit,
                cancellationToken);

        var relevantResults = searchResults
            .Select(
                result =>
                    new RelevantSearchResult(
                        Result: result,
                        Similarity: Math.Clamp(
                            1.0 - result.Distance,
                            0.0,
                            1.0)))
            .Where(result => result.Similarity >= MinimumSimilarity)
            .ToArray();

            _logger.LogInformation(
                "Knowledge retrieval for question: {Question}. " +
                "Returned results: {ResultCount}.",
                question,
                relevantResults.Length);

            for (var index = 0; index < relevantResults.Length; index++)
            {
                var relevantResult = relevantResults[index];
                var result = relevantResult.Result;

                _logger.LogInformation(
                    "Source [{SourceNumber}]: " +
                    "DocumentTitle={DocumentTitle}, " +
                    "SourceId={SourceId}, " +
                    "DocumentId={DocumentId}, " +
                    "ChunkId={ChunkId}, " +
                    "SectionTitle={SectionTitle}, " +
                    "PageNumber={PageNumber}, " +
                    "ChunkIndex={ChunkIndex}, " +
                    "Similarity={Similarity:F4}",
                    index + 1,
                    result.DocumentTitle,
                    result.SourceId,
                    result.DocumentId,
                    result.ChunkId,
                    result.SectionTitle,
                    result.PageNumber,
                    result.ChunkIndex,
                    relevantResult.Similarity);
            }

        if (relevantResults.Length == 0)
        {
            return CreateNoAnswerResponse(question, NoAnswerMessage);
        }

        var llmRequest = new LlmRequest(
            SystemPrompt: BuildSystemPrompt(),
            UserPrompt: BuildUserPrompt(question, relevantResults));

        var decision = await GenerateDecisionAsync(llmRequest, cancellationToken);

        _logger.LogInformation(
            "Grounded-answer decision for question: {Question}. " +
            "CanAnswer={CanAnswer}, " +
            "UsedSourceNumbers={UsedSourceNumbers}, " +
            "Answer={Answer}",
            question,
            decision.CanAnswer,
            string.Join(", ", decision.UsedSourceNumbers ?? []),
            decision.Answer);

        if (IsInconsistentNoAnswerDecision(decision))
        {
            _logger.LogWarning(
                "The model returned an inconsistent no-answer decision. " +
                "Retrying the decision. Question={Question}, Answer={Answer}",
                question,
                decision.Answer);

            decision = await RetryInconsistentDecisionAsync(
                llmRequest,
                decision,
                cancellationToken);

            _logger.LogInformation(
                "Retried grounded-answer decision for question: {Question}. " +
                "CanAnswer={CanAnswer}, " +
                "UsedSourceNumbers={UsedSourceNumbers}, " +
                "Answer={Answer}",
                question,
                decision.CanAnswer,
                string.Join(", ", decision.UsedSourceNumbers ?? []),
                decision.Answer);
        }

        /*
         * A találatok lehetnek témailag relevánsak úgy is,
         * hogy a kérdezett konkrét tényt nem tartalmazzák.
         * Ilyenkor nem adunk vissza forrásalapú választ.
         */
        if (!decision.CanAnswer)
        {
            return CreateNoAnswerResponse(question, decision.Answer);
        }

        if (ContainsDatePlaceholder(decision.Answer))
        {
            _logger.LogWarning(
                "The grounded answer contains an unfilled date placeholder. Returning no-answer. Question={Question}, Answer={Answer}",
                question,
                decision.Answer);

            return CreateNoAnswerResponse(
                question,
                "A forrás csak kitöltetlen dátummezőt tartalmaz, ezért a kért időpont nem állapítható meg.");
        }

        var usedSourceNumbers = NormalizeAndValidateSourceNumbers(decision, relevantResults.Length);

        /*
        * A forrásalapú válasznak legalább egy ténylegesen
        * használt és hivatkozott forrással kell rendelkeznie.
        */
        if (usedSourceNumbers.Length == 0)
        {
            return CreateNoAnswerResponse(
                question,
                "A válaszhoz nem sikerült ellenőrizhető forrást hozzárendelni.");
        }

        /*
        * A modell időnként helyesen megadja a használt
        * forrásszámokat, de az answer mezőből kihagyja
        * a numerikus hivatkozást. Ebben az esetben
        * determinisztikusan hozzáadjuk azokat.
        */
        var normalizedAnswer = EnsureAnswerCitations(
            decision.Answer,
            usedSourceNumbers,
            relevantResults.Length);

        ValidateAnswerCitations(
            normalizedAnswer,
            usedSourceNumbers,
            relevantResults.Length);

        var orderedSourceNumbers = usedSourceNumbers
            .Distinct()
            .OrderBy(sourceNumber => sourceNumber)
            .ToArray();

        var sources = orderedSourceNumbers
            .Select(sourceNumber => relevantResults[sourceNumber - 1])
            .Select(CreateSourceResponse)
            .ToArray();

        var responseAnswer = RenumberAnswerCitations(normalizedAnswer, orderedSourceNumbers);

        return new AskKnowledgeResponse(
            Question: question,
            Answer: responseAnswer,
            AnsweredFromKnowledgeBase: true,
            Sources: sources);
    }

    private static AskKnowledgeResponse CreateNoAnswerResponse(string question, string? answer)
    {
        var normalizedAnswer = string.IsNullOrWhiteSpace(answer) ? NoAnswerMessage : answer.Trim();

        /*
         * No-answer esetben nincs Sources lista, ezért a modell
         * által esetleg mégis visszaadott [n] hivatkozásokat
         * eltávolítjuk.
         */
        normalizedAnswer = NumericCitationRegex().Replace(normalizedAnswer, string.Empty);
        normalizedAnswer = NormalizeWhitespace(normalizedAnswer);

        return new AskKnowledgeResponse(
            Question: question,
            Answer: normalizedAnswer,
            AnsweredFromKnowledgeBase: false,
            Sources: []);
    }

    private static string BuildSystemPrompt()
    {
        return """
            VÁLASZOLHATÓSÁGI SZABÁLYOK

            Mielőtt válaszolsz, döntsd el, hogy a megadott
            források kifejezetten tartalmazzák-e a felhasználó
            által kért információt.

            A témabeli hasonlóság önmagában nem elegendő.
            Attól, hogy egy forrás ugyanazzal a dokumentummal,
            rendszerrel, nyugtával, előleggel, űrlappal vagy
            folyamattal foglalkozik, még nem biztos, hogy
            tartalmazza a kérdezett konkrét adatot.

            A canAnswer értéke legyen false, ha a források nem
            tartalmazzák egyértelműen a kért tényt.

            Soha ne következtess vagy találj ki:
            - dátumot vagy határidőt;
            - megőrzési időt vagy időtartamot;
            - díjat, bírságot, kamatot, kauciót vagy más összeget;
            - bankszámlaszámot;
            - menüpontot vagy navigációs útvonalat;
            - felelős szerepkört;
            - kötelező folyamatlépést.

            Attól, hogy egy díj nincs megemlítve, nem következik,
            hogy a szolgáltatás ingyenes.

            Az üresen kitöltendő sablonmező nem konkrét adat.
            Például a „20.... év ... hó ... napig” szöveg nem
            tekinthető dátumnak vagy határidőnek.

            A bankkártyára történő visszafizetés nem
            bankszámlaszám, és nem bizonyítja, hogy banki
            átutalást kell alkalmazni.

            Ha a kért információ nem állapítható meg:
            - canAnswer legyen false;
            - nevezd meg pontosan, mely információ nem található;
            - usedSourceNumbers legyen üres tömb;
            - az answer mezőben ne használj [n] hivatkozást.

            Ha canAnswer true:
            - kizárólag a forrásokban kifejezetten szereplő
              tényeket használd;
            - minden tényszerű állításhoz adj [n] hivatkozást;
            - csak a ténylegesen felhasznált források sorszámát
              add vissza;
            - ne adj alternatív menüpontot, eljárást vagy
              értelmezést, ha az nem szerepel a forrásban.

            KIZÁRÓLAG egyetlen érvényes JSON-objektumot adj
            vissza. Ne használj Markdown kódblokkot, bevezetőt
            vagy magyarázatot.

            Válaszolható kérdés esetén a JSON szerkezete:

            {
              "canAnswer": true,
              "answer": "A felhasználónak szánt magyar válasz [1].",
              "usedSourceNumbers": [1]
            }

            Nem válaszolható kérdés esetén:

            {
              "canAnswer": false,
              "answer": "A források nem tartalmazzák a kért információt.",
              "usedSourceNumbers": []
            }
            """;
    }

    private static string BuildUserPrompt(string question, IReadOnlyList<RelevantSearchResult> results)
    {
        var builder = new StringBuilder();

        builder.AppendLine("A rendelkezésre álló források:");

        builder.AppendLine();

        for (var index = 0; index < results.Count; index++)
        {
            var relevantResult = results[index];

            var result = relevantResult.Result;

            builder.Append("Forrás [");
            builder.Append(index + 1);
            builder.AppendLine("]");

            builder.Append("Dokumentum: ");
            builder.AppendLine(result.DocumentTitle);

            if (!string.IsNullOrWhiteSpace(result.SectionTitle))
            {
                builder.Append("Szakasz: ");
                builder.AppendLine(result.SectionTitle);
            }

            if (result.PageNumber.HasValue)
            {
                builder.Append("Oldal: ");
                builder.AppendLine(result.PageNumber.Value.ToString());
            }

            builder.Append("Hasonlóság: ");
            builder.AppendLine(relevantResult.Similarity.ToString("F4"));

            builder.AppendLine("Tartalom:");
            builder.AppendLine(result.Content);
            builder.AppendLine();
        }

        builder.AppendLine("Felhasználói kérdés:");

        builder.AppendLine(question);

        builder.AppendLine();

        builder.AppendLine(
            "Először döntsd el, hogy a kérdés a forrásokból " +
            "egyértelműen megválaszolható-e, majd add vissza " +
            "a kért JSON-objektumot.");

        return builder.ToString();
    }

    private static KnowledgeSourceResponse CreateSourceResponse(RelevantSearchResult relevantResult)
    {
        var result = relevantResult.Result;

        return new KnowledgeSourceResponse(
            DocumentId: result.DocumentId,
            ChunkId: result.ChunkId,
            SourceId: result.SourceId,
            DocumentTitle: result.DocumentTitle,
            SourceUrl: result.SourceUrl,
            SectionTitle: result.SectionTitle,
            PageNumber: result.PageNumber,
            ChunkIndex: result.ChunkIndex,
            Similarity: relevantResult.Similarity);
    }

    private static GroundedAnswerDecision ParseDecision(string modelResponse)
    {
        if (string.IsNullOrWhiteSpace(modelResponse))
        {
            throw new InvalidOperationException("The AI model returned an empty response.");
        }

        var normalizedResponse = NormalizeJsonResponse(modelResponse);

        GroundedAnswerDecision? decision;

        try
        {
            decision = JsonSerializer.Deserialize<GroundedAnswerDecision>(normalizedResponse, AnswerJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The AI model returned an invalid grounded-answer response.",
                exception);
        }

        if (decision is null)
        {
            throw new InvalidOperationException("The AI model returned no grounded-answer decision.");
        }

        if (string.IsNullOrWhiteSpace(decision.Answer))
        {
            throw new InvalidOperationException("The grounded-answer decision contains no answer text.");
        }

        var usedSourceNumbers = decision.UsedSourceNumbers ?? [];

        if (!decision.CanAnswer)
        {
            return decision with
            {
                Answer = decision.Answer.Trim(),
                UsedSourceNumbers = []
            };
        }

        return decision with
        {
            Answer = decision.Answer.Trim(),
            UsedSourceNumbers = usedSourceNumbers
        };
    }

    private static int[] NormalizeAndValidateSourceNumbers(GroundedAnswerDecision decision, int sourceCount)
    {
        var sourceNumbers = (decision.UsedSourceNumbers ?? [])
            .Distinct()
            .OrderBy(sourceNumber => sourceNumber)
            .ToArray();

        foreach (var sourceNumber in sourceNumbers)
        {
            if (sourceNumber < 1 || sourceNumber > sourceCount)
            {
                throw new InvalidOperationException(
                    $"The AI model referenced invalid source number {sourceNumber}. Available sources: 1-{sourceCount}.");
            }
        }

        return sourceNumbers;
    }

    private static void ValidateAnswerCitations(
        string answer,
        IReadOnlyCollection<int> usedSourceNumbers,
        int availableSourceCount)
    {
        var citedSourceNumbers = ExtractCitationNumbers(answer);

        if (citedSourceNumbers.Count == 0)
        {
            throw new InvalidOperationException("The grounded answer contains no numeric source citation.");
        }

        foreach (var citedSourceNumber in citedSourceNumbers)
        {
            if (citedSourceNumber < 1 || citedSourceNumber > availableSourceCount)
            {
                throw new InvalidOperationException($"The grounded answer contains invalid citation [{citedSourceNumber}].");
            }

            if (!usedSourceNumbers.Contains(citedSourceNumber))
            {
                throw new InvalidOperationException($"The answer cites source [{citedSourceNumber}], but that source is missing from usedSourceNumbers.");
            }
        }

        foreach (var sourceNumber in usedSourceNumbers)
        {
            if (!citedSourceNumbers.Contains(sourceNumber))
            {
                throw new InvalidOperationException(
                    $"Source {sourceNumber} is listed in usedSourceNumbers, but the answer does not contain citation [{sourceNumber}].");
            }
        }
    }

    private static HashSet<int>ExtractCitationNumbers(string answer)
    {
        var sourceNumbers = new HashSet<int>();

        foreach (Match match in NumericCitationRegex().Matches(answer))
        {
            var citationContent = match.Groups[1].Value;

            foreach (var value in citationContent.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries
                         | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(value, out var sourceNumber))
                {
                    sourceNumbers.Add(sourceNumber);
                }
            }
        }

        return sourceNumbers;
    }

    private static string ExtractJsonObject(string response)
    {
        var trimmed = response.Trim();
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');

        if (firstBrace < 0 || lastBrace <= firstBrace)
        {
            return trimmed;
        }

        return trimmed[firstBrace..(lastBrace + 1)];
    }

    private static string NormalizeWhitespace(string value)
    {
        return WhitespaceRegex()
            .Replace(
                value,
                " ")
            .Trim();
    }

    private static string GetQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must not be empty.", nameof(question));
        }

        var normalizedQuestion = question.Trim();

        if (normalizedQuestion.Length > MaximumQuestionLength)
        {
            throw new ArgumentException(
                $"Question must not exceed {MaximumQuestionLength} characters.",
                nameof(question));
        }

        return normalizedQuestion;
    }

    private static void ValidateRetrievalLimit(int limit)
    {
        if (limit is < 1 or > MaximumRetrievalLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Retrieval limit must be between 1 and {MaximumRetrievalLimit}.");
        }
    }

    private static string EnsureAnswerCitations(
        string answer,
        IReadOnlyCollection<int> usedSourceNumbers,
        int availableSourceCount)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return answer;
        }

        var validSourceNumbers = usedSourceNumbers
            .Where(sourceNumber =>
                sourceNumber >= 1 &&
                sourceNumber <= availableSourceCount)
            .Distinct()
            .OrderBy(sourceNumber => sourceNumber)
            .ToArray();

        if (validSourceNumbers.Length == 0)
        {
            return answer.Trim();
        }

        var normalizedAnswer = answer.Trim();
        var citedSourceNumbers = ExtractCitationNumbers(normalizedAnswer);

        var missingSourceNumbers = validSourceNumbers
            .Where(sourceNumber => !citedSourceNumbers.Contains(sourceNumber))
            .ToArray();

        if (missingSourceNumbers.Length == 0)
        {
            return normalizedAnswer;
        }

        var missingCitations = string.Join(
            " ",
            missingSourceNumbers.Select(sourceNumber => $"[{sourceNumber}]"));

        return $"{normalizedAnswer} {missingCitations}";
    }

    private static string NormalizeJsonResponse(string modelResponse)
    {
        if (string.IsNullOrWhiteSpace(modelResponse))
        {
            throw new InvalidOperationException(
                "The AI model returned an empty grounded-answer response.");
        }

        var normalized = modelResponse.Trim();

        /*
        * Markdown JSON-kódblokk eltávolítása.
        * A modell például ezt adhatja vissza:
        *
        * ```json
        * { "canAnswer": true }
        * ```
        */
        if (normalized.StartsWith(
                "```json",
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["```json".Length..];
        }
        else if (normalized.StartsWith(
                    "```",
                    StringComparison.Ordinal))
        {
            normalized = normalized[3..];
        }

        normalized = normalized.Trim();

        if (normalized.EndsWith(
                "```",
                StringComparison.Ordinal))
        {
            normalized = normalized[..^3];
        }

        normalized = normalized.Trim();

        /*
        * A modell néha JSON-stringként kódolja
        * a teljes JSON-objektumot:
        *
        * "{\"canAnswer\":true,...}"
        */
        if (normalized.StartsWith("\"", StringComparison.Ordinal) &&
            normalized.EndsWith("\"", StringComparison.Ordinal))
        {
            try
            {
                var unescaped = JsonSerializer.Deserialize<string>(normalized);

                if (!string.IsNullOrWhiteSpace(unescaped))
                {
                    normalized = unescaped.Trim();
                }
            }
            catch (JsonException)
            {
                /*
                * Nem szabályos JSON-string. A későbbi
                * objektum-deszerializálás ad pontos hibát.
                */
            }
        }

        /*
        * Egyes kisebb modellek külső string-idézőjelek
        * nélkül escape-elik az objektum idézőjeleit:
        *
        * {\"canAnswer\":true,...}
        */
        if (normalized.StartsWith("{\\\"", StringComparison.Ordinal) ||
            normalized.StartsWith("[\\\"", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\\\"", "\"");
        }

        /*
        * Ha a modell bevezető vagy záró szöveget adott,
        * csak az első JSON-objektumot tartjuk meg.
        */
        normalized = ExtractJsonObject(normalized);

        return normalized;
    }

    private async Task<GroundedAnswerDecision> GenerateDecisionAsync(LlmRequest initialRequest, CancellationToken cancellationToken)
    {
        var initialResponse = await _llmService.GenerateAsync(initialRequest, cancellationToken);

        try
        {
            return ParseDecision(initialResponse.Content);
        }
        catch (InvalidOperationException exception)
            when (exception.InnerException is JsonException)
        {
            var repairRequest = new LlmRequest(
                SystemPrompt: BuildJsonRepairSystemPrompt(),
                UserPrompt: BuildJsonRepairUserPrompt(
                    initialResponse.Content));

            var repairedResponse = await _llmService.GenerateAsync(
                repairRequest,
                cancellationToken);

            return ParseDecision(repairedResponse.Content);
        }
    }

    private static string BuildJsonRepairSystemPrompt()
    {
        return """
        A feladatod egy hibás strukturált válasz javítása.

        KIZÁRÓLAG egyetlen érvényes JSON-objektumot adj vissza.
        Ne használj Markdown-kódblokkot, bevezetőt vagy magyarázatot.

        A JSON pontos szerkezete:

        {
        "canAnswer": true vagy false,
        "answer": "rövid magyar válasz",
        "usedSourceNumbers": [1, 2]
        }

        Követelmények:

        - minden property legyen jelen;
        - a canAnswer legyen JSON boolean;
        - az answer legyen lezárt JSON string;
        - a usedSourceNumbers csak egész számokat tartalmazzon;
        - a tömbelemeket vesszővel válaszd el;
        - ne adj hozzá új tényt;
        - ne változtasd meg az eredeti válasz jelentését;
        - ha az eredeti tartalom nem állítható helyre megbízhatóan,
        állítsd a canAnswer értékét false-ra,
        az usedSourceNumbers értékét pedig üres tömbre.
        """;
    }

    private static string BuildJsonRepairUserPrompt(string invalidModelResponse)
    {
        return $"""
        Javítsd szabályos JSON-objektummá az alábbi hibás választ.

        HIBÁS VÁLASZ KEZDETE

        {invalidModelResponse}

        HIBÁS VÁLASZ VÉGE
        """;
    }

    private static bool ContainsDatePlaceholder(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        /*
        * Kitöltetlen dátummezők felismerése, például:
        *
        * 20.... év ... hó ... nap
        * 20....… ………......…… hó ……...… napig
        */
        var normalized = answer
            .Replace('…', '.')
            .Replace(" ", string.Empty);

        return normalized.Contains("20...", StringComparison.Ordinal) &&
            normalized.Contains("hó", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("nap", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInconsistentNoAnswerDecision(GroundedAnswerDecision decision)
    {
        if (decision.CanAnswer)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(decision.Answer))
        {
            return false;
        }

        var answer = decision.Answer.Trim();

        var explicitNoAnswerMarkers = new[]
        {
            "nem található",
            "nem tartalmazza",
            "nem tartalmazzák",
            "nem állapítható meg",
            "nem derül ki",
            "nincs információ",
            "nincs adat",
            "nem adható válasz"
        };

        return !explicitNoAnswerMarkers.Any(marker => answer.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<GroundedAnswerDecision> RetryInconsistentDecisionAsync(
        LlmRequest originalRequest,
        GroundedAnswerDecision inconsistentDecision,
        CancellationToken cancellationToken)
    {
        var retryRequest = new LlmRequest(
            SystemPrompt: BuildSystemPrompt(),
            UserPrompt: $"""
            {originalRequest.UserPrompt}

            AZ ELŐZŐ DÖNTÉS INKONZISZTENS VOLT

            Az előző döntés:

            canAnswer: {inconsistentDecision.CanAnswer}
            answer: {inconsistentDecision.Answer}
            usedSourceNumbers: [{string.Join(
                ", ",
                inconsistentDecision.UsedSourceNumbers ?? [])}]

            Az előző válasz konkrét állítást tartalmazott, miközben
            a canAnswer értéke false volt.

            Értékeld újra a kérdést az eredeti források alapján.

            Ha a konkrét állítást valamelyik forrás egyértelműen
            alátámasztja:

            - canAnswer legyen true;
            - az answer tartalmazza a választ;
            - használd a megfelelő [n] hivatkozást;
            - a usedSourceNumbers tartalmazza ugyanazt az n értéket.

            Ha a konkrét állítást egyik forrás sem támasztja alá:

            - canAnswer legyen false;
            - az answer világosan mondja ki, hogy a kért információ
            nem található;
            - usedSourceNumbers legyen üres tömb.

            KIZÁRÓLAG egyetlen érvényes JSON-objektumot adj vissza.
            """);

        return await GenerateDecisionAsync(
            retryRequest,
            cancellationToken);
    }

    private static string RenumberAnswerCitations(string answer, IReadOnlyList<int> usedSourceNumbers)
    {
        if (string.IsNullOrWhiteSpace(answer) || usedSourceNumbers.Count == 0)
        {
            return answer.Trim();
        }

        var citationNumberMap = usedSourceNumbers
            .Distinct()
            .OrderBy(sourceNumber => sourceNumber)
            .Select(
                (originalSourceNumber, index) =>
                    new
                    {
                        OriginalSourceNumber = originalSourceNumber,
                        NewSourceNumber = index + 1
                    })
            .ToDictionary(
                item => item.OriginalSourceNumber,
                item => item.NewSourceNumber);

        return NumericCitationRegex().Replace(
            answer,
            match =>
            {
                var originalNumbers = match.Groups[1].Value
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Select(
                        value => int.TryParse(value, out var number)
                            ? number
                            : (int?)null)
                    .Where(number => number.HasValue)
                    .Select(number => number!.Value)
                    .ToArray();

                var renumberedNumbers = originalNumbers
                    .Where(citationNumberMap.ContainsKey)
                    .Select(number => citationNumberMap[number])
                    .Distinct()
                    .OrderBy(number => number)
                    .ToArray();

                return renumberedNumbers.Length == 0
                    ? string.Empty
                    : $"[{string.Join(", ", renumberedNumbers)}]";
            });
    }

    [GeneratedRegex(@"\[\s*(\d+(?:\s*,\s*\d+)*)\s*\]", RegexOptions.CultureInvariant)]
    private static partial Regex NumericCitationRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record RelevantSearchResult(
        SemanticSearchResult Result,
        double Similarity);
}