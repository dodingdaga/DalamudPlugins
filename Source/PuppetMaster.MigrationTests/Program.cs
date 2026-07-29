using System.Text.Json;
using System.Text.RegularExpressions;
using PuppetMaster;

var options = new JsonSerializerOptions
{
    IncludeFields = true,
    PropertyNameCaseInsensitive = true,
};

Run("PuppetMaster_v0.json", configuration =>
{
    Assert(configuration.Version == 2, "v0 should migrate to v2");
    Assert(configuration.Reactions.Count == 1, "v0 should create one reaction");
    var reaction = configuration.Reactions[0];
    Assert(reaction.TriggerPhrase == "please do", "v0 trigger should be preserved");
    Assert(reaction.EnabledChannels.Contains(10), "v0 enabled Say channel should be preserved");
    Assert(reaction.CommandBlacklist.SequenceEqual(["/sit", "/groundsit", "/lounge"]), "v0 sit rules should normalize");
    Assert(configuration.ShowReactionNotifications, "v0 should receive the v2 notification default");
    Assert(!configuration.ShowSuppressedReactionNotifications, "v0 should keep suppression notifications off by default");
    Assert(configuration.DefaultCommandWhitelist.Count == 0, "v0 should receive an empty default whitelist");
    Assert(configuration.DefaultCommandBlacklist.SequenceEqual(["/sit", "/groundsit", "/lounge"]), "v0 should receive safe command defaults");
    Assert(!configuration.DefaultAllowAllCommands && configuration.DefaultMotionOnly, "v0 should receive safe command behavior defaults");
    Assert(configuration.DefaultEnabledChannels.Count == 0, "v0 should receive empty channel defaults");
    Assert(reaction.ExecutionPolicy == ReactionExecutionPolicy.QueueEveryTrigger,
        "v0 reaction should preserve legacy retrigger behavior");
});

Run("PuppetMaster_v1.json", configuration =>
{
    Assert(configuration.Version == 2, "v1 should migrate to v2");
    Assert(configuration.ShowReactionNotifications, "v1 should enable notification default");
    Assert(!configuration.ShowSuppressedReactionNotifications, "v1 should keep suppression notifications off by default");
    Assert(configuration.Reactions[0].AllowAllCommands, "v1 AllowAllCommands should be preserved");
    Assert(configuration.Reactions[0].EnabledChannels.SequenceEqual([10, 14]), "v1 channels should be preserved");
    Assert(configuration.DefaultCommandBlacklist.SequenceEqual(["/sit", "/groundsit", "/lounge"]), "v1 should receive safe command defaults");
    Assert(!configuration.DefaultAllowAllCommands && configuration.DefaultMotionOnly, "v1 should receive safe command behavior defaults");
    Assert(configuration.DefaultEnabledChannels.Count == 0, "v1 should receive empty channel defaults");
    Assert(configuration.Reactions[0].ExecutionPolicy == ReactionExecutionPolicy.QueueEveryTrigger,
        "v1 reaction should preserve legacy retrigger behavior");
});

Run("PuppetMaster_v2_legacy.json", configuration =>
{
    Assert(configuration.Version == 2, "v2 should stay v2");
    Assert(!configuration.ShowReactionNotifications, "existing v2 notification choice should be preserved");
    Assert(configuration.ShowSuppressedReactionNotifications, "existing v2 suppression notification choice should be preserved");
    var reaction = configuration.Reactions[0];
    Assert(reaction.AllowSit, "legacy sit marker should be normalized");
    Assert(reaction.AllowAllCommands, "existing AllowAllCommands should be preserved");
    Assert(reaction.CooldownSeconds == 0, "negative cooldown should normalize to zero");
    Assert(reaction.ExecutionPolicy == ReactionExecutionPolicy.QueueEveryTrigger,
        "existing v2 reaction without an execution policy should preserve legacy retrigger behavior");
    Assert(reaction.CommandWhitelist.SequenceEqual(["/echo"]), "whitelist should deduplicate case-insensitively");
    Assert(reaction.CommandBlacklist.SequenceEqual(["/sit", "/groundsit", "/lounge"]), "blacklist should deduplicate and add legacy sit rules");
    Assert(configuration.DefaultCommandWhitelist.SequenceEqual(["/echo"]), "default whitelist should deduplicate case-insensitively");
    Assert(configuration.DefaultCommandBlacklist.SequenceEqual(["/sit", "/groundsit"]), "default blacklist should deduplicate case-insensitively");
    Assert(configuration.DefaultEnabledChannels.SequenceEqual([10, 14]), "default channels should deduplicate");
    Assert(configuration.CustomChannels.Count == 1 &&
           configuration.CustomChannels[0].ChatType == 77 &&
           configuration.CustomChannels[0].Name == "First",
        "custom channels should remove invalid and duplicate IDs while preserving the first entry");

    var created = Reaction.CreateDefault(
        commandWhitelist: configuration.DefaultCommandWhitelist,
        commandBlacklist: configuration.DefaultCommandBlacklist,
        allowAllCommands: configuration.DefaultAllowAllCommands,
        motionOnly: configuration.DefaultMotionOnly,
        enabledChannels: configuration.DefaultEnabledChannels);
    configuration.DefaultCommandWhitelist.Add("/wait");
    configuration.DefaultEnabledChannels.Add(57);
    Assert(created.CommandWhitelist.SequenceEqual(["/echo"]), "new reactions should copy rather than share command defaults");
    Assert(created.AllowAllCommands, "new reactions should copy the allow-all default");
    Assert(!created.MotionOnly, "new reactions should copy the emote motion default");
    Assert(created.EnabledChannels.SequenceEqual([10, 14]), "new reactions should copy rather than share channel defaults");
    Assert(created.ExecutionPolicy == ReactionExecutionPolicy.IgnoreWhileRunning,
        "new reactions should use the safe execution policy default");
});

Run("PuppetMaster_v2_null_collections.json", configuration =>
{
    Assert(configuration.Version == 2, "null-collection fixture should stay v2");
    Assert(configuration.EnabledChannels.Count == 0, "null enabled channels should normalize to an empty list");
    Assert(configuration.CustomChannels.Count == 0, "null custom channels should normalize to an empty list");
    Assert(configuration.Reactions.Count == 0, "null reactions should normalize to an empty list");
    Assert(configuration.DefaultCommandWhitelist.Count == 0,
        "null default whitelist should normalize to an empty list");
    Assert(configuration.DefaultCommandBlacklist.SequenceEqual(["/sit", "/groundsit", "/lounge"]),
        "null default blacklist should normalize to safe defaults");
    Assert(configuration.DefaultEnabledChannels.Count == 0,
        "null default channels should normalize to an empty list");
});

var future = new Configuration { Version = ConfigVersion.CURRENT + 1 };
AssertThrows<InvalidOperationException>(() => ConfigurationMigrator.MigrateAndNormalize(future), "future config should be rejected");

RunExecutionGateTests();
RunReactionCommandMatcherTests();
RunRetriggerQueueTests();
RunRetriggerSchedulerTests();
RunConfigurationUpgradeTransactionTests(options);

Console.WriteLine("All PuppetMaster configuration migration tests passed.");
return;

void Run(string fixtureName, Action<Configuration> assertions)
{
    var path = Path.Combine(AppContext.BaseDirectory, "TestConfigs", fixtureName);
    var configuration = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path), options)
        ?? throw new InvalidOperationException($"Could not deserialize {fixtureName}");
    ConfigurationMigrator.MigrateAndNormalize(configuration);
    assertions(configuration);
    var normalizedJson = JsonSerializer.Serialize(configuration, options);
    var changedAgain = ConfigurationMigrator.MigrateAndNormalize(configuration);
    Assert(!changedAgain, $"{fixtureName} migration should be idempotent");
    Assert(JsonSerializer.Serialize(configuration, options) == normalizedJson, $"{fixtureName} should not change on a second pass");
    Console.WriteLine($"PASS {fixtureName}");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException($"Assertion failed: {message}");
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"Assertion failed: {message}");
}

static void RunExecutionGateTests()
{
    var gate = new ReactionExecutionGate();
    var reaction = new Reaction();
    const long startedAt = 1_000_000;
    var second = System.Diagnostics.Stopwatch.Frequency;

    Assert(gate.TryEnter(reaction, TimeSpan.FromSeconds(10), startedAt, out var first, out var firstReason),
        "first reaction run should enter");
    Assert(firstReason == ReactionRejectionReason.None, "accepted run should have no rejection reason");
    Assert(!gate.TryEnter(reaction, TimeSpan.FromSeconds(10), startedAt + 20 * second, out _, out var runningReason),
        "single-flight should win even after cooldown expires");
    Assert(runningReason == ReactionRejectionReason.Busy, "active run should reject as busy");

    first!.Dispose();
    Assert(gate.TryEnter(reaction, TimeSpan.FromSeconds(10), startedAt + 20 * second, out var secondLease, out _),
        "reaction should run immediately after a long run when cooldown already expired");
    secondLease!.Dispose();

    var cooldownReaction = new Reaction();
    Assert(gate.TryEnter(cooldownReaction, TimeSpan.FromSeconds(10), startedAt, out var cooldownLease, out _),
        "cooldown test run should enter");
    cooldownLease!.Dispose();
    Assert(!gate.TryEnter(cooldownReaction, TimeSpan.FromSeconds(10), startedAt + 5 * second, out _, out var cooldownReason),
        "completed reaction should remain blocked during cooldown");
    Assert(cooldownReason == ReactionRejectionReason.Cooldown, "early retrigger should reject as cooldown");
    Assert(gate.TryEnter(cooldownReaction, TimeSpan.FromSeconds(10), startedAt + 10 * second, out var finalLease, out _),
        "reaction should enter at cooldown boundary");
    finalLease!.Dispose();

    var priorityGate = new ReactionExecutionGate();
    var priorityReaction = new Reaction();
    Assert(priorityGate.TryEnter(
            priorityReaction,
            TimeSpan.Zero,
            System.Diagnostics.Stopwatch.GetTimestamp(),
            out var activeLease,
            out _),
        "priority test should acquire its initial lease");
    var queuedLeaseTask = priorityGate.EnterWhenAvailableAsync(
        priorityReaction,
        TimeSpan.Zero,
        CancellationToken.None);
    Assert(!queuedLeaseTask.IsCompleted, "queued entrant should wait for the active run");
    Assert(!priorityGate.TryEnter(
            priorityReaction,
            TimeSpan.Zero,
            System.Diagnostics.Stopwatch.GetTimestamp(),
            out _,
            out var priorityReason),
        "fresh trigger should not bypass an existing queued entrant");
    Assert(priorityReason == ReactionRejectionReason.Busy,
        "fresh trigger behind a queued entrant should be treated as busy");
    activeLease!.Dispose();
    var queuedLease = queuedLeaseTask.GetAwaiter().GetResult();
    queuedLease.Dispose();

    Console.WriteLine("PASS reaction execution gate");
}

static void RunReactionCommandMatcherTests()
{
    var lookbehindPattern = new Regex(
        @"(?<=Boss uses )(Fire)",
        RegexOptions.None,
        TimeSpan.FromMilliseconds(250));
    var status = ReactionCommandMatcher.TryGenerateCommand(
        lookbehindPattern,
        "Boss uses Fire",
        "/echo $1",
        out var command,
        out var error);

    Assert(status == ReactionMatchStatus.Success, "lookbehind match should generate a command");
    Assert(command == "/echo Fire", "replacement should use captures from the original match");
    Assert(error is null, "successful replacement should not report an error");

    var invalidStatus = ReactionCommandMatcher.TryGenerateCommand(
        new Regex("(Fire)", RegexOptions.None, TimeSpan.FromMilliseconds(250)),
        "Fire",
        "$2147483648",
        out _,
        out var invalidError);

    Assert(invalidStatus == ReactionMatchStatus.InvalidReplacement, "malformed replacement should be rejected");
    Assert(!string.IsNullOrWhiteSpace(invalidError), "malformed replacement should explain the error");

    Console.WriteLine("PASS reaction command matcher");
}

static void RunRetriggerQueueTests()
{
    var ignored = new BoundedRetriggerQueue<string>(3);
    Assert(ignored.Enqueue(ReactionExecutionPolicy.IgnoreWhileRunning, "A") == 0,
        "ignored retrigger should not count as an overload drop");
    Assert(ignored.Count == 0, "ignore policy should not queue a retrigger");

    var latest = new BoundedRetriggerQueue<string>(3);
    latest.Enqueue(ReactionExecutionPolicy.QueueLatestTrigger, "A");
    Assert(latest.TryPeek(out var observedLatest) && observedLatest == "A",
        "drainer should inspect the next retrigger without removing it");
    latest.Enqueue(ReactionExecutionPolicy.QueueLatestTrigger, "B");
    Assert(latest.Count == 1, "latest policy should keep one retrigger");
    Assert(latest.TryDequeue(out var latestItem) && latestItem == "B",
        "latest policy should keep the newest retrigger");

    var every = new BoundedRetriggerQueue<string>(3);
    every.Enqueue(ReactionExecutionPolicy.QueueEveryTrigger, "A");
    every.Enqueue(ReactionExecutionPolicy.QueueEveryTrigger, "B");
    every.Enqueue(ReactionExecutionPolicy.QueueEveryTrigger, "C");
    Assert(every.Enqueue(ReactionExecutionPolicy.QueueEveryTrigger, "D") == 1,
        "bounded queue should report one dropped oldest retrigger");
    Assert(every.TryDequeue(out var first) && first == "B", "bounded queue should drop the oldest item");
    Assert(every.TryDequeue(out var second) && second == "C", "queue-every should preserve FIFO order");
    Assert(every.TryDequeue(out var third) && third == "D", "queue-every should retain the newest item");

    every.Enqueue(ReactionExecutionPolicy.QueueEveryTrigger, "E");
    every.Clear();
    Assert(every.Count == 0, "clearing a retrigger queue should remove all pending work");

    Console.WriteLine("PASS bounded retrigger queue");
}

static void RunRetriggerSchedulerTests()
{
    var gateOpened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var acquireStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var executed = new List<string>();
    var scheduler = new BoundedRetriggerScheduler<string>(
        16,
        async (_, cancellationToken) =>
        {
            acquireStarted.TrySetResult();
            await gateOpened.Task.WaitAsync(cancellationToken);
            return new CancellationTokenSource();
        },
        (item, lease) =>
        {
            lease.Dispose();
            executed.Add(item);
            return Task.CompletedTask;
        });

    var drainer = scheduler.Enqueue(
        ReactionExecutionPolicy.QueueLatestTrigger,
        "A",
        CancellationToken.None)!;
    acquireStarted.Task.GetAwaiter().GetResult();
    scheduler.Enqueue(ReactionExecutionPolicy.QueueLatestTrigger, "B", CancellationToken.None);
    Assert(scheduler.PendingCount == 1, "waiting latest scheduler should keep exactly one visible item");
    gateOpened.TrySetResult();
    drainer.GetAwaiter().GetResult();
    Assert(executed.SequenceEqual(["B"]),
        "latest scheduler should execute only the newest item that arrived while waiting");

    var neverOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var cancelledExecutions = 0;
    var cancellableScheduler = new BoundedRetriggerScheduler<string>(
        16,
        async (_, cancellationToken) =>
        {
            await neverOpen.Task.WaitAsync(cancellationToken);
            return new CancellationTokenSource();
        },
        (_, lease) =>
        {
            lease.Dispose();
            cancelledExecutions++;
            return Task.CompletedTask;
        });
    var cancelledDrainer = cancellableScheduler.Enqueue(
        ReactionExecutionPolicy.QueueEveryTrigger,
        "never",
        CancellationToken.None)!;
    cancellableScheduler.Cancel();
    cancelledDrainer.GetAwaiter().GetResult();
    Assert(cancellableScheduler.PendingCount == 0, "cancelling a scheduler should clear pending work");
    Assert(cancelledExecutions == 0, "cancelling while waiting should not execute pending work");
    var restartedDrainer = cancellableScheduler.Enqueue(
        ReactionExecutionPolicy.QueueEveryTrigger,
        "after cancel",
        CancellationToken.None)!;
    neverOpen.TrySetResult();
    restartedDrainer.GetAwaiter().GetResult();
    Assert(cancelledExecutions == 1, "scheduler should accept new work after cancellation");

    var fifoGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var fifoExecuted = new List<int>();
    var overflowDrops = 0;
    var fifoScheduler = new BoundedRetriggerScheduler<int>(
        16,
        async (_, cancellationToken) =>
        {
            await fifoGate.Task.WaitAsync(cancellationToken);
            return new CancellationTokenSource();
        },
        (item, lease) =>
        {
            lease.Dispose();
            fifoExecuted.Add(item);
            return Task.CompletedTask;
        },
        dropped => overflowDrops += dropped);
    Task? fifoDrainer = null;
    for (var item = 1; item <= 17; item++)
    {
        var startedDrainer = fifoScheduler.Enqueue(
            ReactionExecutionPolicy.QueueEveryTrigger,
            item,
            CancellationToken.None);
        fifoDrainer ??= startedDrainer;
    }
    Assert(fifoScheduler.PendingCount == 16,
        $"scheduler should include its waiting front item in the 16-item bound (actual {fifoScheduler.PendingCount})");
    Assert(overflowDrops == 1,
        $"scheduler should report one drop when its exact bound is exceeded (actual {overflowDrops})");
    fifoGate.TrySetResult();
    fifoDrainer!.GetAwaiter().GetResult();
    Assert(fifoExecuted.SequenceEqual(Enumerable.Range(2, 16)),
        "queue-every scheduler should drop the oldest and preserve FIFO order");

    Exception? reportedFailure = null;
    var reportedDiscarded = -1;
    var failureScheduler = new BoundedRetriggerScheduler<string>(
        16,
        (_, _) => throw new InvalidOperationException("scheduler test failure"),
        (_, lease) =>
        {
            lease.Dispose();
            return Task.CompletedTask;
        },
        reportFailure: (exception, discarded) =>
        {
            reportedFailure = exception;
            reportedDiscarded = discarded;
        });
    var failedDrainer = failureScheduler.Enqueue(
        ReactionExecutionPolicy.QueueEveryTrigger,
        "A",
        CancellationToken.None)!;
    failureScheduler.Enqueue(ReactionExecutionPolicy.QueueEveryTrigger, "B", CancellationToken.None);
    failedDrainer.GetAwaiter().GetResult();
    Assert(reportedFailure is InvalidOperationException,
        "unexpected scheduler failures should be reported");
    Assert(reportedDiscarded >= 1, "scheduler failure should report discarded pending work");
    Assert(failureScheduler.PendingCount == 0, "scheduler failure should clear its unusable backlog");

    Console.WriteLine("PASS bounded retrigger scheduler");
}

static void RunConfigurationUpgradeTransactionTests(JsonSerializerOptions serializerOptions)
{
    var directory = Path.Combine(Path.GetTempPath(), $"PuppetMasterMigrationTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var sourceFixture = Path.Combine(AppContext.BaseDirectory, "TestConfigs", "PuppetMaster_v1.json");
        var originalBytes = File.ReadAllBytes(sourceFixture);
        var activePath = Path.Combine(directory, "PuppetMaster.json");
        File.WriteAllBytes(activePath, originalBytes);
        var configuration = JsonSerializer.Deserialize<Configuration>(originalBytes, serializerOptions)
            ?? throw new InvalidOperationException("Could not deserialize transaction test configuration.");
        var fixedTime = new DateTime(2026, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc);

        var backupPath = ConfigurationUpgradeTransaction.Execute(
            activePath,
            configuration.Version,
            ConfigVersion.CURRENT,
            () => ConfigurationMigrator.MigrateAndNormalize(configuration),
            () => File.WriteAllText(activePath, JsonSerializer.Serialize(configuration, serializerOptions)),
            fixedTime);

        Assert(backupPath != null && File.Exists(backupPath), "v1 upgrade should create a recoverable backup");
        Assert(File.ReadAllBytes(backupPath!).SequenceEqual(originalBytes),
            "backup should be byte-for-byte identical to the original v1 file");
        var activeConfiguration = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(activePath), serializerOptions)
            ?? throw new InvalidOperationException("Could not deserialize migrated active configuration.");
        Assert(activeConfiguration.Version == ConfigVersion.CURRENT,
            "successful transaction should save the migrated v2 configuration as active");

        var collisionPath = Path.Combine(directory, "Collision.json");
        var collisionOriginal = "{\"Version\":1,\"Marker\":\"original\"}";
        File.WriteAllText(collisionPath, collisionOriginal);
        var expectedCollisionBackup = Path.Combine(
            directory,
            "Collision.v1.20260102030405006.backup.json");
        File.WriteAllText(expectedCollisionBackup, "existing backup");
        var collisionPrepared = false;
        var collisionSaved = false;
        AssertThrows<IOException>(() => ConfigurationUpgradeTransaction.Execute(
                collisionPath,
                1,
                ConfigVersion.CURRENT,
                () => collisionPrepared = true,
                () => collisionSaved = true,
                fixedTime),
            "backup collision should fail closed");
        Assert(!collisionPrepared && !collisionSaved,
            "backup failure should prevent both migration preparation and save");
        Assert(File.ReadAllText(collisionPath) == collisionOriginal,
            "backup failure should leave the active source file untouched");

        var failurePath = Path.Combine(directory, "MigrationFailure.json");
        var failureOriginal = "{\"Version\":1,\"Marker\":\"untouched\"}";
        File.WriteAllText(failurePath, failureOriginal);
        var failureSaved = false;
        AssertThrows<InvalidOperationException>(() => ConfigurationUpgradeTransaction.Execute(
                failurePath,
                1,
                ConfigVersion.CURRENT,
                () => throw new InvalidOperationException("simulated migration failure"),
                () => failureSaved = true,
                fixedTime.AddSeconds(1)),
            "migration failure should propagate");
        Assert(!failureSaved, "migration failure should prevent save");
        Assert(File.ReadAllText(failurePath) == failureOriginal,
            "migration failure should leave the active source file untouched");
        Assert(Directory.GetFiles(directory, "MigrationFailure.v1.*.backup.json").Length == 1,
            "migration failure should still leave the original recoverable backup");

        Console.WriteLine("PASS configuration upgrade transaction");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}
