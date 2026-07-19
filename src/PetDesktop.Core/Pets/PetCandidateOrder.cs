using System.Collections.ObjectModel;

namespace PetDesktop.Core.Pets;

public static class PetCandidateOrder
{
    public static IReadOnlyList<PetDescriptor> PreferSaved(
        IEnumerable<PetDescriptor> candidates,
        string? savedPetId)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var snapshot = candidates.ToArray();
        if (string.IsNullOrEmpty(savedPetId))
        {
            return Array.AsReadOnly(snapshot);
        }

        var matching = snapshot.Where(candidate => string.Equals(candidate.Id, savedPetId, StringComparison.Ordinal));
        var remaining = snapshot.Where(candidate => !string.Equals(candidate.Id, savedPetId, StringComparison.Ordinal));
        return new ReadOnlyCollection<PetDescriptor>(matching.Concat(remaining).ToArray());
    }
}
