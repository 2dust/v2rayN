using ServiceLib.Common;
using ServiceLib.Handler;
using ServiceLib.Manager;
using ServiceLib.Models.Entities;
using ServiceLib.Services;
using v2rayN.Web.Contracts;

namespace v2rayN.Web.Services;

public sealed partial class V2rayRuntime
{
    private CancellationTokenSource? _scheduledOperationsCancellation;
    private Task? _scheduledOperationsTask;

    private void StartScheduledOperations(CancellationToken hostToken)
    {
        _scheduledOperationsCancellation = CancellationTokenSource.CreateLinkedTokenSource(hostToken, _operations.ShutdownToken);
        var cancellationToken = _scheduledOperationsCancellation.Token;
        _scheduledOperationsTask = Task.Run(() => RunScheduledOperationsAsync(cancellationToken));
    }

    private async Task<bool> StopScheduledOperationsAsync(CancellationToken shutdownToken)
    {
        var cancellation = _scheduledOperationsCancellation;
        var task = _scheduledOperationsTask;
        if (cancellation is null || task is null)
        {
            return true;
        }

        cancellation.Cancel();
        try
        {
            await task.WaitAsync(shutdownToken);
        }
        catch (OperationCanceledException) when (task.IsCompleted && cancellation.IsCancellationRequested)
        {
            // Expected during graceful service shutdown.
        }
        finally
        {
            if (task.IsCompleted)
            {
                cancellation.Dispose();
                _scheduledOperationsCancellation = null;
                _scheduledOperationsTask = null;
            }
        }

        return true;
    }

    private async Task RunScheduledOperationsAsync(CancellationToken cancellationToken)
    {
        var elapsedMinutes = 1;
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            await RunScheduledStepAsync("subscription", RunScheduledSubscriptionUpdatesAsync, cancellationToken);

            if (elapsedMinutes % 20 == 0)
            {
                await RunScheduledStepAsync("save", async token =>
                {
                    await using var operation = await _operations.EnterOperationAsync(token);
                    await _mutations.RunAsync(async () =>
                    {
                        await EnsureConfigSaveSucceededAsync(() => ConfigHandler.SaveConfig(Config));
                        await ProfileExManager.Instance.SaveTo();
                    }, operation.Token);
                }, cancellationToken);
            }

            if (elapsedMinutes % 60 == 0)
            {
                await RunScheduledStepAsync("cleanup", async token =>
                {
                    await using var operation = await _operations.EnterOperationAsync(token);
                    FileUtils.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1), "Test");
                    FileUtils.DeleteExpiredFiles(Utils.GetLogPath(), DateTime.Now.AddDays(-7));
                    FileUtils.DeleteExpiredFiles(Utils.GetTempPath(), DateTime.Now.AddDays(-7));
                }, cancellationToken);

                var elapsedHours = elapsedMinutes / 60;
                if (Config.GuiItem.AutoUpdateInterval > 0 && elapsedHours % Config.GuiItem.AutoUpdateInterval == 0)
                {
                    await RunScheduledStepAsync("geo-update", RunScheduledGeoUpdateAsync, cancellationToken);
                }
            }

            if (elapsedMinutes % 1440 == 1)
            {
                await RunScheduledStepAsync("core-update-check", RunScheduledCoreUpdateCheckAsync, cancellationToken);
            }
            elapsedMinutes++;
        }
    }

    private async Task RunScheduledStepAsync(string name, Func<CancellationToken, Task> step, CancellationToken cancellationToken)
    {
        try
        {
            await step(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AddLog("task", $"{name}: {exception.Message}");
        }
    }

    private async Task RunScheduledSubscriptionUpdatesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        IReadOnlyList<SubItem> subscriptions;
        await using (var operation = await _operations.EnterOperationAsync(cancellationToken))
        {
            subscriptions = (await AppManager.Instance.SubItems() ?? [])
                .Where(item => item.AutoUpdateInterval > 0
                               && now - item.UpdateTime >= item.AutoUpdateInterval * 60L)
                .ToArray();
        }

        foreach (var subscription in subscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var update = StartSubscriptionUpdateTask(subscription.Id, useProxy: true, cancellationToken);
            if (update is not null)
            {
                await update.WaitAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private async Task RunScheduledGeoUpdateAsync(CancellationToken cancellationToken)
    {
        await using var operation = await _operations.EnterExclusiveAsync(
            cancellationToken,
            allowReadOnlyObservations: true);
        await new UpdateService(Config, (success, message) =>
        {
            AddLog("update", message);
            _events.Publish("geo-update-progress", new
            {
                success,
                code = success ? "ok" : "geo_update_progress",
                messageKey = success ? ApiMessageKeys.CommonCompleted : ApiMessageKeys.GeoUpdateProgress,
                rawLog = message,
            });
            if (success)
            {
                _events.Publish("geo-update-completed", OperationView.Ok(ApiMessageKeys.CommonCompleted));
            }
            return Task.CompletedTask;
        }).UpdateGeoFileAll(blProxy: true, cancellationToken: operation.Token);
    }

    private async Task RunScheduledCoreUpdateCheckAsync(CancellationToken cancellationToken)
    {
        await using var operation = await _operations.EnterOperationAsync(cancellationToken);
        var updateService = new UpdateService(Config, (_, _) => Task.CompletedTask);
        var messages = await updateService.CheckHasUpdateOnlyAll(
            Config.CheckUpdateItem.CheckPreReleaseUpdate,
            Config.CheckUpdateItem.UpdateViaProxy,
            operation.Token);
        foreach (var message in messages)
        {
            AddLog("update", message);
        }
    }
}
