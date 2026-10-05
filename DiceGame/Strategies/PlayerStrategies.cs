using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace DiceGame.Strategies;

public sealed class PlayerStrategies
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Assemblies containing discovered strategies are preserved by the application trimming configuration.")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Public strategy constructors are preserved by the application trimming configuration.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Public strategy constructors are preserved by the application trimming configuration.")]
    public PlayerStrategies(params Assembly[] additionalAssemblies)
    {
        Available = additionalAssemblies.Prepend(typeof(IPlayerStrategy).Assembly).Distinct().SelectMany(assembly => assembly.GetTypes()).Where(type => type.IsClass && type.IsAbstract is false && type.ContainsGenericParameters is false && typeof(IPlayerStrategy).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null).Select(type => (IPlayerStrategy)Activator.CreateInstance(type)!).OrderBy(strategy => strategy.Name).ToList().AsReadOnly();
    }

    public IReadOnlyList<IPlayerStrategy> Available { get; }

    public IPlayerStrategy Get(string name) => Available.Single(strategy => strategy.Name == name);
}
