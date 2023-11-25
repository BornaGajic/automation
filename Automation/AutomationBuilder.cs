using System.Threading.Tasks.Dataflow;

namespace Automation
{
    public class AutomationBuilder : IAutomationBuilder
    {
        public AutomationBuilder()
        {
            Progress = (stageName, stageNumber) => ValueTask.CompletedTask;
        }

        public AutomationBuilder(Func<string, int, ValueTask> progress)
        {
            Progress = progress;
        }

        private CancellationTokenSource CancellationTokenSrc { get; } = new();
        private Func<string, int, ValueTask> Progress { get; }

        public IAutomationBuilderStage<TData, TData> Step<TData>(Func<CancellationToken, TData> callback, string name = "")
        {
            return Step(cancellation => ValueTask.FromResult(callback(cancellation)), name);
        }

        public IAutomationBuilderStage<TData, TData> Step<TData>(Func<CancellationToken, ValueTask<TData>> callback, string name = "")
        {
            var bufferBlock = new BufferBlock<TData>(new DataflowBlockOptions
            {
                CancellationToken = CancellationTokenSrc.Token
            });

            Func<CancellationToken, ValueTask<TData>> modifiedWithProgress = async cancel =>
            {
                await Progress(name, 1);
                return await callback(cancel);
            };

            return new AutomationBuilderStage<TData, TData>(1, modifiedWithProgress, bufferBlock, bufferBlock, CancellationTokenSrc, Progress);
        }

        protected class Automation<TData>(
            ITargetBlock<TData> head,
            IDataflowBlock tail,
            Func<CancellationToken, ValueTask<TData>> dataCallback,
            CancellationTokenSource cancellationTokenSrc
        ) : IAutomation
        {
            public void Cancel(int? millisecondsDelay = null)
            {
                if (millisecondsDelay is not null)
                {
                    cancellationTokenSrc.CancelAfter(millisecondsDelay.Value);
                }
                else
                {
                    cancellationTokenSrc.Cancel();
                }
            }

            public async Task<AutomationState> StartAsync()
            {
                var data = await dataCallback(cancellationTokenSrc.Token);

                head.Post(data);
                head.Complete();

                return new AutomationState
                {
                    Completion = tail.Completion
                };
            }
        }

        protected class AutomationBuilderStage<TData, TSource>(
            int stageNumber,
            Func<CancellationToken, ValueTask<TData>> dataCallback,
            ITargetBlock<TData> head,
            ISourceBlock<TSource> Tail,
            CancellationTokenSource cancelTokenSource,
            Func<string, int, ValueTask> progress
        ) : IAutomationBuilderStage<TData, TSource>
        {
            protected DataflowLinkOptions LinkOptions = new() { PropagateCompletion = true };
            protected ISourceBlock<TSource> Tail = Tail;

            public IAutomation Build()
            {
                Tail?.LinkTo(DataflowBlock.NullTarget<TSource>(), LinkOptions);

                return new Automation<TData>(head, Tail, dataCallback, cancelTokenSource);
            }

            public IAutomationBuilderStage<TData, TSource> Step(Func<TSource, CancellationToken, ValueTask> callback, string name = "")
            {
                stageNumber++;

                var actionBlock = new TransformBlock<TSource, TSource>(async step =>
                {
                    await progress(name, stageNumber);
                    await callback(step, cancelTokenSource.Token);
                    return step;
                }, new ExecutionDataflowBlockOptions
                {
                    CancellationToken = cancelTokenSource.Token
                });

                var newStage = new AutomationBuilderStage<TData, TSource>(
                    stageNumber, dataCallback, head, actionBlock, cancelTokenSource, progress
                );

                Tail.LinkTo(newStage.Tail as ITargetBlock<TSource>, LinkOptions);

                return newStage;
            }

            public IAutomationBuilderStage<TData, TSource> Step(Action<TSource> callback, string name = "")
            {
                return Step((item, cancellation) =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    callback(item);
                    return ValueTask.CompletedTask;
                }, name);
            }

            public IAutomationBuilderStage<TData, TSource> Step(Func<TSource, ValueTask> callback, string name = "")
            {
                return Step(async (item, cancellation) =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    await callback(item);
                }, name);
            }

            public IAutomationBuilderStage<TData, TResult> Step<TResult>(Func<TSource, TResult> callback, string name = "")
            {
                return Step((item, cancellation) =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(callback(item));
                }, name);
            }

            public IAutomationBuilderStage<TData, TResult> Step<TResult>(Func<TSource, ValueTask<TResult>> callback, string name = "")
            {
                return Step(async (item, cancellation) =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    return await callback(item);
                }, name);
            }

            public IAutomationBuilderStage<TData, TResult> Step<TResult>(Func<TSource, CancellationToken, ValueTask<TResult>> callback, string name = "")
            {
                stageNumber++;

                var transformBlock = new TransformBlock<TSource, TResult>(async step =>
                {
                    await progress(name, stageNumber);
                    return await callback(step, cancelTokenSource.Token);
                }, new ExecutionDataflowBlockOptions
                {
                    CancellationToken = cancelTokenSource.Token
                });

                var newStage = new AutomationBuilderStage<TData, TResult>(
                    stageNumber, dataCallback, head, transformBlock, cancelTokenSource, progress
                );

                Tail.LinkTo(newStage.Tail as ITargetBlock<TSource>, LinkOptions);

                return newStage;
            }
        }
    }
}