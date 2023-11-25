namespace Automation;

public record AutomationState
{
    public Guid Id { get; } = Guid.NewGuid();
    public Task Completion { get; init; }
}