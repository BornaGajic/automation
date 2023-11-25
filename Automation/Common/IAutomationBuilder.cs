namespace Automation;

public interface IAutomationBuilder
{
    IAutomationBuilderStage<TData, TData> Step<TData>(Func<CancellationToken, TData> callback, string name = "");

    IAutomationBuilderStage<TData, TData> Step<TData>(Func<CancellationToken, ValueTask<TData>> callback, string name = "");
}