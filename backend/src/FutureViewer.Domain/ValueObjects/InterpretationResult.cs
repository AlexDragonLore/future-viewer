namespace FutureViewer.Domain.ValueObjects;

public sealed record InterpretationResult(string Text, string Model, DateTime GeneratedAt);
