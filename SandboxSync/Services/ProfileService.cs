using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SandboxSync.Models;

namespace SandboxSync.Services;

public sealed class ProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _profilesDirectory;
    private readonly string _settingsPath;

    public ProfileService()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SandboxSync");

        _profilesDirectory = Path.Combine(root, "Profiles");
        _settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public async Task SaveProfileAsync(SyncProfile profile, CancellationToken cancellationToken = default)
    {
        var safeName = SanitizeFileName(profile.Name);
        var path = Path.Combine(_profilesDirectory, $"{safeName}.json");
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SyncProfile?> LoadProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_profilesDirectory, $"{SanitizeFileName(name)}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SyncProfile>(json, JsonOptions);
    }

    public IReadOnlyList<string> ListProfiles()
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            return [];
        }

        return Directory.GetFiles(_profilesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SaveSettingsAsync(SyncSettings settings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SyncSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new SyncSettings();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SyncSettings>(json, JsonOptions) ?? new SyncSettings();
        }
        catch
        {
            return new SyncSettings();
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim();
    }
}
