namespace DjVisualizer.Application.Abstractions;

/// <summary>
/// One ops signal. The sink decides how (or whether) to deliver it. The render path must not
/// depend on delivery succeeding.
/// </summary>
public sealed record OpsEvent(string Service, string Level, string Message, DateTime Timestamp);
