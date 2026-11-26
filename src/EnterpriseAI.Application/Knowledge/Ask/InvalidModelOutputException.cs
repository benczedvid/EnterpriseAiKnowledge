namespace EnterpriseAI.Application.Knowledge.Ask;

/*
 * A nyelvi modell kimenete nem dolgozható fel vagy nem
 * ellenőrizhető (hibás JSON, érvénytelen hivatkozás stb.).
 * Az AskKnowledgeHandler ezt kezelt no-answer válasszá alakítja.
 */
public sealed class InvalidModelOutputException : InvalidOperationException
{
    public InvalidModelOutputException(string message)
        : base(message)
    {
    }

    public InvalidModelOutputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
