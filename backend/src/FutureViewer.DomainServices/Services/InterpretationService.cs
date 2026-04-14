using FutureViewer.Domain.Entities;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class InterpretationService
{
    private readonly IAIInterpreter _interpreter;

    public InterpretationService(IAIInterpreter interpreter)
    {
        _interpreter = interpreter;
    }

    public Task<InterpretationResult> InterpretAsync(
        Reading reading,
        Spread spread,
        CancellationToken ct = default)
    {
        return _interpreter.InterpretAsync(reading, spread, ct);
    }
}
