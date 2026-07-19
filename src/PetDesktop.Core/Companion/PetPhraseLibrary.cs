namespace PetDesktop.Core.Companion;

/// <summary>
/// The phrases a user has tailored for one logical pet package.
/// </summary>
public sealed record PetPhraseLibrary(
    string[]? CustomQuotes = null,
    string[]? WellnessBuiltInPrompts = null,
    string[]? WellnessCustomPrompts = null,
    string[]? BuiltInQuotes = null)
{
    public static PetPhraseLibrary Empty { get; } = new([], null, []);
}

public static class PetPhraseLibraries
{
    public const string DefaultPetId = "default";

    public static string NormalizePetId(string? petId) =>
        string.IsNullOrWhiteSpace(petId) ? DefaultPetId : petId.Trim();

    /// <summary>
    /// Creates a profile for a newly seen pet. Legacy shared phrases are assigned
    /// only to the first profile, so adding another pet never copies them across.
    /// </summary>
    public static Dictionary<string, PetPhraseLibrary> EnsureProfile(
        IReadOnlyDictionary<string, PetPhraseLibrary>? profiles,
        string? petId,
        PetPhraseLibrary legacyPhrases)
    {
        var result = profiles is null
            ? new Dictionary<string, PetPhraseLibrary>(StringComparer.Ordinal)
            : new Dictionary<string, PetPhraseLibrary>(profiles, StringComparer.Ordinal);
        var key = NormalizePetId(petId);
        if (!result.ContainsKey(key))
        {
            result[key] = result.Count == 0 ? legacyPhrases : PetPhraseLibrary.Empty;
        }

        return result;
    }
}
