using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Jobs;

public sealed class RotationSpeed
{
    public const double MinSecondsPerRotation = 2.0;
    public const double MaxSecondsPerRotation = 15.0;
    public const double DefaultSecondsPerRotation = 3.0;

    public double SecondsPerRotation { get; }

    private RotationSpeed(double secondsPerRotation) => SecondsPerRotation = secondsPerRotation;

    public static RotationSpeed Default { get; } = new(DefaultSecondsPerRotation);

    public static RotationSpeed Create(double secondsPerRotation)
    {
        if (secondsPerRotation is < MinSecondsPerRotation or > MaxSecondsPerRotation)
        {
            throw new InvalidRotationSpeedException(
                $"Rotation speed must be between {MinSecondsPerRotation} and {MaxSecondsPerRotation} seconds per rotation.");
        }

        return new RotationSpeed(secondsPerRotation);
    }

    public override bool Equals(object? obj) => obj is RotationSpeed other && SecondsPerRotation.Equals(other.SecondsPerRotation);

    public override int GetHashCode() => SecondsPerRotation.GetHashCode();
}
