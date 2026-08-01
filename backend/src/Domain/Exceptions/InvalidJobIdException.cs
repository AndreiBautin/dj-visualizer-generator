namespace DjVisualizer.Domain.Exceptions;

public sealed class InvalidJobIdException(string value)
    : DomainException($"'{value}' is not a valid job id.");
