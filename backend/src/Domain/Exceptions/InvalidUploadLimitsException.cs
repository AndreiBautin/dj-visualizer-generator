namespace DjVisualizer.Domain.Exceptions;

public sealed class InvalidUploadLimitsException(string reason)
    : DomainException(reason);
