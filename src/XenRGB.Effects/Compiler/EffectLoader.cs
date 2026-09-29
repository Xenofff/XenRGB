using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using XenRGB.SDK;

namespace XenRGB.Effects.Compiler;

/// <summary>
/// Loads an effect from a folder containing manifest.json and effect.cs.
/// </summary>
public static class EffectLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<(IEffect? Effect, string? Error)> LoadFromDirectoryAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return (null, $"Directory does not exist: {directoryPath}");
        }

        string sourceFile = Path.Combine(directoryPath, "effect.cs");
        string manifestFile = Path.Combine(directoryPath, "manifest.json");

        if (!File.Exists(sourceFile))
        {
            return (null, $"Missing effect.cs in {directoryPath}");
        }

        EffectManifest? manifest = null;
        if (File.Exists(manifestFile))
        {
            try
            {
                string json = await File.ReadAllTextAsync(manifestFile).ConfigureAwait(false);
                manifest = JsonSerializer.Deserialize<EffectManifest>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                return (null, $"Invalid manifest.json: {ex.Message}");
            }
        }

        string sourceCode = await File.ReadAllTextAsync(sourceFile).ConfigureAwait(false);
        string folderName = Path.GetFileName(directoryPath);
        var (effect, err) = EffectCompiler.CompileEffectSource(sourceCode, $"XenRGB.Effect.{folderName}");

        if (effect != null && manifest != null && effect.Manifest != null)
        {
            // Sync manifest overrides if specified
            if (!string.IsNullOrEmpty(manifest.Id)) effect.Manifest.Id = manifest.Id;
            if (!string.IsNullOrEmpty(manifest.Name)) effect.Manifest.Name = manifest.Name;
            if (!string.IsNullOrEmpty(manifest.Author)) effect.Manifest.Author = manifest.Author;
            if (!string.IsNullOrEmpty(manifest.Description)) effect.Manifest.Description = manifest.Description;
            if (manifest.DefaultParameters != null) effect.Manifest.DefaultParameters = manifest.DefaultParameters;
        }

        return (effect, err);
    }
}
