using PetDesktop.Core.Companion;

namespace PetDesktop.Core.Tests;

public sealed class PetPhraseLibrariesTests
{
    [Fact]
    public void EnsureProfileMigratesLegacyPhrasesToFirstPetOnly()
    {
        var legacy = new PetPhraseLibrary(["legacy quote"], ["legacy wellness"], ["custom wellness"]);

        var first = PetPhraseLibraries.EnsureProfile(null, "cat", legacy);
        var second = PetPhraseLibraries.EnsureProfile(first, "dog", legacy);

        Assert.Equal(["legacy quote"], first["cat"].CustomQuotes!);
        Assert.Equal(["legacy wellness"], first["cat"].WellnessBuiltInPrompts!);
        Assert.Equal(["custom wellness"], first["cat"].WellnessCustomPrompts!);
        Assert.Empty(second["dog"].CustomQuotes!);
        Assert.Empty(second["dog"].WellnessCustomPrompts!);
        Assert.Null(second["dog"].WellnessBuiltInPrompts);
    }

    [Fact]
    public void EnsureProfilePreservesExistingPetProfile()
    {
        var existing = new Dictionary<string, PetPhraseLibrary>
        {
            ["cat"] = new(["cat quote"]),
        };

        var result = PetPhraseLibraries.EnsureProfile(existing, "cat", new(["legacy quote"]));

        Assert.Equal(["cat quote"], result["cat"].CustomQuotes!);
        Assert.Single(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NormalizePetIdUsesDefaultForBlankIds(string? petId) =>
        Assert.Equal(PetPhraseLibraries.DefaultPetId, PetPhraseLibraries.NormalizePetId(petId));
}
