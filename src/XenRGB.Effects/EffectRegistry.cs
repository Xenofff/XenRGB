using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XenRGB.Effects.BuiltIn;
using XenRGB.Effects.Compiler;
using XenRGB.SDK;

namespace XenRGB.Effects;

/// <summary>
/// Registry storing all built-in and external dynamically compiled effects.
/// </summary>
public sealed class EffectRegistry
{
    private readonly ConcurrentDictionary<string, IEffect> _effects = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? RegistryChanged;

    public IReadOnlyList<IEffect> AllEffects => _effects.Values.ToList();

    public EffectRegistry()
    {
        RegisterBuiltInEffects();
    }

    public void RegisterBuiltInEffects()
    {
        Register(new CottonCandyEffect());
        Register(new PlainColorEffect());
        Register(new BiohazardEffect());
        Register(new RainbowEffect());
    }

    public void Register(IEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _effects[effect.Id] = effect;
        RegistryChanged?.Invoke(this, EventArgs.Empty);
    }

    public IEffect? GetEffect(string id)
    {
        return _effects.TryGetValue(id, out var effect) ? effect : null;
    }

    /// <summary>
    /// Scans a directory for subfolders containing effect.cs and manifest.json.
    /// </summary>
    public async Task<int> LoadEffectsFromDirectoryAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return 0;

        int loadedCount = 0;
        foreach (var subDir in Directory.GetDirectories(directoryPath))
        {
            var (effect, error) = await EffectLoader.LoadFromDirectoryAsync(subDir).ConfigureAwait(false);
            if (effect != null)
            {
                Register(effect);
                loadedCount++;
            }
        }

        return loadedCount;
    }
}
