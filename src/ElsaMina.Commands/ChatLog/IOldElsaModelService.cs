namespace ElsaMina.Commands.ChatLog;

public interface IOldElsaModelService
{
    /// <summary>
    /// Generates a random sentence from the precompiled "old Elsa" Markov model.
    /// Returns <c>null</c> when no sentence could be generated.
    /// </summary>
    string GenerateSentence();

    /// <summary>
    /// Generates a sentence from the precompiled "old Elsa" Markov model that starts with the
    /// given words. Returns <c>null</c> when no sentence could be generated.
    /// </summary>
    string GenerateSentence(string start);
}
