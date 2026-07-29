using System.Text.Json;
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
});

Run("PuppetMaster_v1.json", configuration =>
{
    Assert(configuration.Version == 2, "v1 should migrate to v2");
    Assert(configuration.ShowReactionNotifications, "v1 should enable notification default");
    Assert(configuration.Reactions[0].AllowAllCommands, "v1 AllowAllCommands should be preserved");
    Assert(configuration.Reactions[0].EnabledChannels.SequenceEqual([10, 14]), "v1 channels should be preserved");
});

Run("PuppetMaster_v2_legacy.json", configuration =>
{
    Assert(configuration.Version == 2, "v2 should stay v2");
    Assert(!configuration.ShowReactionNotifications, "existing v2 notification choice should be preserved");
    var reaction = configuration.Reactions[0];
    Assert(reaction.AllowSit, "legacy sit marker should be normalized");
    Assert(reaction.AllowAllCommands, "existing AllowAllCommands should be preserved");
    Assert(reaction.CommandWhitelist.SequenceEqual(["/echo"]), "whitelist should deduplicate case-insensitively");
    Assert(reaction.CommandBlacklist.SequenceEqual(["/sit", "/groundsit", "/lounge"]), "blacklist should deduplicate and add legacy sit rules");
});

var future = new Configuration { Version = ConfigVersion.CURRENT + 1 };
AssertThrows<InvalidOperationException>(() => ConfigurationMigrator.MigrateAndNormalize(future), "future config should be rejected");

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
