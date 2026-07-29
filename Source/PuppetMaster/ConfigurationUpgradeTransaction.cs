using System;
using System.IO;

namespace PuppetMaster;

internal static class ConfigurationUpgradeTransaction
{
    public static string? Execute(
        string configPath,
        int sourceVersion,
        int targetVersion,
        Action prepareAndValidate,
        Action save,
        DateTime? utcNow = null)
    {
        string? backupPath = null;
        if (sourceVersion < targetVersion && File.Exists(configPath))
        {
            var timestamp = (utcNow ?? DateTime.UtcNow).ToString("yyyyMMddHHmmssfff");
            var backupName = $"{Path.GetFileNameWithoutExtension(configPath)}.v{sourceVersion}.{timestamp}.backup.json";
            backupPath = Path.Combine(Path.GetDirectoryName(configPath)!, backupName);
            File.Copy(configPath, backupPath, overwrite: false);
        }

        prepareAndValidate();
        save();
        return backupPath;
    }
}
