namespace Automation;

public interface IAutomationBuilderStage<TData, TSource>
{
    IAutomation Build();

    IAutomationBuilderStage<TData, TSource> Step(Func<TSource, CancellationToken, ValueTask> callback, string name = "");

    IAutomationBuilderStage<TData, TResult> Step<TResult>(Func<TSource, CancellationToken, ValueTask<TResult>> callback, string name = "");

    IAutomationBuilderStage<TData, TSource> Step(Func<TSource, ValueTask> callback, string name = "");

    IAutomationBuilderStage<TData, TResult> Step<TResult>(Func<TSource, ValueTask<TResult>> callback, string name = "");

    IAutomationBuilderStage<TData, TSource> Step(Action<TSource> callback, string name = "");

    IAutomationBuilderStage<TData, TResult> Step<TResult>(Func<TSource, TResult> callback, string name = "");
}