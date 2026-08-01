namespace DjVisualizer.Domain.Exceptions;

public sealed class InvalidJobTitleException(string reason)
    : DomainException(reason);
