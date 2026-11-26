namespace EnterpriseAI.Application.Abstractions.AI;

/*
 * ResponseJsonSchema: opcionális JSON Schema. Ha meg van adva,
 * az LLM-szolgáltatás strukturált kimenetet kér, így a modell
 * válasza a sémának megfelelő, érvényes JSON lesz.
 */
public sealed record LlmRequest(
    string SystemPrompt,
    string UserPrompt,
    string? ResponseJsonSchema = null);
