namespace DjVisualizer.Domain.Exceptions;

public sealed class InvalidJobStateTransitionException(string currentStatus, string attemptedAction)
    : DomainException($"Cannot {attemptedAction} a job that is currently {currentStatus}.");
