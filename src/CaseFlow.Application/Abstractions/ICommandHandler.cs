namespace CaseFlow.Application.Abstractions;

// Deliberately not MediatR. Explicit handler interfaces give the same
// command/query separation without the indirection - you can F12 from a
// controller straight into the use case that serves it.
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
