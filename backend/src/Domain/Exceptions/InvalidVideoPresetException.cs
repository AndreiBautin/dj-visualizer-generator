namespace DjVisualizer.Domain.Exceptions;

public sealed class InvalidVideoPresetException(string name)
    : DomainException($"'{name}' is not a supported video preset.");
