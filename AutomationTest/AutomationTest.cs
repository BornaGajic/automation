using FluentAssertions;
using System.Text;
using Automation;
using Xunit;

namespace tag.framework.test
{
    public class AutomationTest
    {
        [Fact(DisplayName = $"T01: Automation")]
        public async Task T01()
        {
            var hasProcessedToEnd = false;

            var automation = await new AutomationBuilder()
                .Step(async cancellation =>
                {
                    Console.WriteLine("| Loading Data");

                    await Task.Delay(50);

                    return Enumerable.Range(1, 3).ToList();
                })
                .Step(async data =>
                {
                    Console.WriteLine("| Mapping");

                    await Task.Delay(50);

                    return data.Select(x => x + 1);
                })
                .Step(async data =>
                {
                    Console.WriteLine("| Validating");

                    var childAutomation = await new AutomationBuilder()
                        .Step(cancellation => data)
                        .Step(async data =>
                        {
                            foreach (var item in data)
                            {
                                Console.WriteLine($"|\tChecking item number {item}.");
                                await Task.Delay(50);
                            }
                        })
                        .Build()
                        .StartAsync();

                    await childAutomation.Completion;
                })
                .Step(async data =>
                {
                    Console.WriteLine("| Calculating");

                    var childAutomationState = await new AutomationBuilder()
                        .Step(cancellation => data)
                        .Step(async data =>
                        {
                            foreach (var item in data)
                            {
                                Console.WriteLine($"|\tCalculating {item}.");
                                await Task.Delay(50);

                                var result = item;

                                var leafAutomationState = await new AutomationBuilder()
                                       .Step(cancellation => item)
                                       .Step(async item =>
                                       {
                                           foreach (var _ in Enumerable.Range(1, Random.Shared.Next(2, 5)))
                                           {
                                               Console.WriteLine($"|\t\tBombastic mathematics...");
                                               await Task.Delay(50);
                                           }

                                           result = item * 2;
                                       })
                                       .Build()
                                       .StartAsync();

                                await leafAutomationState.Completion;

                                Console.WriteLine($"|\tDone, result = {result}.");
                            }
                        })
                        .Build()
                        .StartAsync();

                    await childAutomationState.Completion;
                })
                .Step(result =>
                {
                    Console.WriteLine("| Done!");

                    result.Should().NotBeEmpty();
                    hasProcessedToEnd = true;
                })
                .Build()
                .StartAsync();

            await automation.Completion;

            hasProcessedToEnd.Should().BeTrue();
        }

        [Fact(DisplayName = $"T02: Cancel Automation - Initial Data")]
        public async Task T02()
        {
            var isCanceled = false;

            var automation = new AutomationBuilder()
                .Step(async cancel =>
                {
                    try
                    {
                        await Task.Delay(1_000, cancel);
                    }
                    catch (OperationCanceledException)
                    {
                        isCanceled = true;
                        throw;
                    }

                    return -1;
                })
                .Build();

            var automationStateTask = automation.StartAsync();

            try
            {
                await Task.Delay(50);

                automation.Cancel();

                await (await automationStateTask).Completion;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                isCanceled.Should().BeTrue();
            }
        }

        [Fact(DisplayName = $"T03: Cancel Automation Steps")]
        public async Task T03()
        {
            var isCanceled = false;

            var automation = new AutomationBuilder()
                .Step(cancel => 20)
                .Step(async (count, cancellation) =>
                {
                    try
                    {
                        foreach (var item in Enumerable.Range(1, count))
                        {
                            cancellation.ThrowIfCancellationRequested();
                            await Task.Delay(500);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        isCanceled = true;
                        throw;
                    }

                    return -1;
                })
                .Build();

            var automationStateTask = automation.StartAsync();

            try
            {
                await Task.Delay(1_000);

                automation.Cancel();

                await (await automationStateTask).Completion;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                isCanceled.Should().BeTrue();
            }
        }

        [Fact(DisplayName = $"T04: Cancel Child Automation")]
        public async Task T04()
        {
            var isChildCanceled = false;
            var parentStepsContinue = false;

            var automation = new AutomationBuilder()
                .Step(cancel => ValueTask.FromResult(20))
                .Step(async (item, parentCancellation) =>
                {
                    var childAutomation = new AutomationBuilder()
                        .Step(cancel => ValueTask.FromResult(10))
                        .Step(async (count, childCancellation) =>
                        {
                            foreach (var item in Enumerable.Range(1, count))
                            {
                                childCancellation.ThrowIfCancellationRequested();
                                await Task.Delay(500);
                            }
                        })
                        .Build();

                    var childAutomationStateTask = childAutomation.StartAsync();

                    try
                    {
                        await Task.Delay(1_000);

                        childAutomation.Cancel();

                        await (await childAutomationStateTask).Completion;
                    }
                    catch (OperationCanceledException)
                    {
                        isChildCanceled = true;
                    }

                    return -1;
                })
                .Step(x =>
                {
                    parentStepsContinue = true;
                })
                .Build();


            await (await automation.StartAsync()).Completion;

            isChildCanceled.Should().BeTrue();
            parentStepsContinue.Should().BeTrue();
        }

        [Fact(DisplayName = $"T05: Automation Progress")]
        public async Task T05()
        {
            var allMessages = new StringBuilder();

            Func<string, int, ValueTask> progress = async (stepName, stepNumber) =>
            {
                await Task.Delay(10);
                allMessages.AppendLine($"{stepNumber}. Automation: {stepName}");
            };

            var automationState = await new AutomationBuilder(progress)
                .Step(cancel => 5, "result = 5")
                .Step(number => 5 + 5, "result = 10")
                .Step(async result =>
                {
                    Func<string, int, ValueTask> childProgress = async (stepName, stepNumber) =>
                    {
                        await Task.Delay(10);
                        allMessages.AppendLine($"{stepNumber}. Child Automation: {stepName}");
                    };

                    var childAutomationState = await new AutomationBuilder(childProgress)
                        .Step(cancel => result, $"result = {result}")
                        .Step(result => result + 1, $"result = {result} + 1")
                        .Build()
                        .StartAsync();

                    await childAutomationState.Completion;
                }, "Processing result in child automation.")
                .Build()
                .StartAsync();

            await automationState.Completion;

            var final = allMessages.ToString();
            final.Should().NotBeNullOrEmpty();
        }
    }
}