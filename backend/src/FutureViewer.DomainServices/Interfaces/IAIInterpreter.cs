using FutureViewer.Domain.Entities;
using FutureViewer.Domain.ValueObjects;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAIInterpreter
{
    Task<InterpretationResult> InterpretAsync(
        Reading reading,
        Spread spread,
        CancellationToken ct = default);
}
