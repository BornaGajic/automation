namespace Automation;

public interface IAutomation
{
    /// <summary>
    /// Cancels every automation step.
    /// </summary>
    /// <param name="millisecondsDelay">Delay cancellation.</param>
    void Cancel(int? millisecondsDelay = null);

    /// <returns>Automation completion Task.</returns>
    Task<AutomationState> StartAsync();
}