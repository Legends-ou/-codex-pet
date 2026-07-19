namespace PetDesktop.Core.Pets;

public sealed class PetFormatException : Exception
{
    public PetFormatException(string message)
        : base(message)
    {
    }
}
