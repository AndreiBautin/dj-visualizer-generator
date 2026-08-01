namespace DjVisualizer.Domain.Exceptions;

public sealed class InvalidRotationSpeedException(string reason) : DomainException(reason);
