using DjVisualizer.Application.Abstractions;

namespace DjVisualizer.Api.IntegrationTests.Support;

public sealed class StubAudioProbe(Func<TimeSpan> durationProvider) : IAudioProbe
{
    public Task<TimeSpan> GetDurationAsync(string filePath, CancellationToken cancellationToken) =>
        Task.FromResult(durationProvider());
}
