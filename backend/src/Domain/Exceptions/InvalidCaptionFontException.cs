namespace DjVisualizer.Domain.Exceptions;

public sealed class InvalidCaptionFontException(string name)
    : DomainException($"'{name}' is not a supported caption font.");
