namespace Shop.CatalogService.Application.Catalog.Commands;

public enum CommandStatus
{
    Success,
    NotFound,
    Conflict,
    BadRequest
}

public sealed class CommandResult<T>
{
    public CommandStatus Status { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }

    private CommandResult(CommandStatus status, T? value, string? errorMessage)
    {
        Status = status;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public static CommandResult<T> Success(T value) => new(CommandStatus.Success, value, null);
    public static CommandResult<T> NotFound(string message = "Entity not found.") => new(CommandStatus.NotFound, default, message);
    public static CommandResult<T> Conflict(string message) => new(CommandStatus.Conflict, default, message);
    public static CommandResult<T> BadRequest(string message) => new(CommandStatus.BadRequest, default, message);
}

public sealed class CommandResult
{
    public CommandStatus Status { get; }
    public string? ErrorMessage { get; }

    private CommandResult(CommandStatus status, string? errorMessage)
    {
        Status = status;
        ErrorMessage = errorMessage;
    }

    public static CommandResult Success() => new(CommandStatus.Success, null);
    public static CommandResult NotFound(string message = "Entity not found.") => new(CommandStatus.NotFound, message);
    public static CommandResult Conflict(string message) => new(CommandStatus.Conflict, message);
    public static CommandResult BadRequest(string message) => new(CommandStatus.BadRequest, message);
}
