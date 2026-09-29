using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XenRGB.SDK;

namespace XenRGB.Effects.Compiler;

/// <summary>
/// Compiles C# effect source code at runtime into XenRGB.SDK.IEffect instances.
/// </summary>
public static class EffectCompiler
{
    public static (IEffect? Effect, string? ErrorMessage) CompileEffectSource(string sourceCode, string? assemblyName = null)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return (null, "Source code is empty.");
        }

        assemblyName ??= $"XenRGB.UserEffect.{Guid.NewGuid():N}";

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // Core references required for compiling effect code
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(MathF).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEffect).Assembly.Location) // XenRGB.SDK
        };

        // Add System.Runtime assembly if available
        var sysRuntime = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (sysRuntime != null && !string.IsNullOrEmpty(sysRuntime.Location))
        {
            references.Add(MetadataReference.CreateFromFile(sysRuntime.Location));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"Line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}");
            return (null, string.Join(Environment.NewLine, errors));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        var effectType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IEffect).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        if (effectType == null)
        {
            return (null, "No class implementing IEffect was found in the source code.");
        }

        try
        {
            var instance = (IEffect?)Activator.CreateInstance(effectType);
            return (instance, null);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to instantiate effect class: {ex.Message}");
        }
    }
}
