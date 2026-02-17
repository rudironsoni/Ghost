using System.Reflection;

namespace Ghost.Architecture.Tests;

/// <summary>
/// Helper methods for architecture testing using reflection instead of NetArchTest.
/// NetArchTest has compatibility issues with .NET 10.
/// </summary>
public static class ArchitectureTestHelpers
{
    /// <summary>
    /// Checks if any type in the source namespace depends on types in the target namespace.
    /// Uses assembly-level reference checking for high-level dependencies,
    /// then type-level checking for specific cross-namespace usage.
    /// </summary>
    public static bool HasDependencyOn(string sourceNamespacePrefix, string targetNamespacePrefix)
    {
        // Get assemblies that contain types in the source namespace
        Assembly[] sourceAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Ghost.") == true)
            .Where(a => ContainsTypesInNamespace(a, sourceNamespacePrefix))
            .ToArray();

        // Get assemblies that contain types in the target namespace
        Assembly[] targetAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Ghost.") == true)
            .Where(a => ContainsTypesInNamespace(a, targetNamespacePrefix))
            .ToArray();

        // Check if any source assembly references any target assembly
        foreach (Assembly sourceAssembly in sourceAssemblies)
        {
            AssemblyName[] referencedAssemblies = sourceAssembly.GetReferencedAssemblies();

            foreach (AssemblyName referencedAssembly in referencedAssemblies)
            {
                // Check if this referenced assembly is one of our target assemblies
                if (targetAssemblies.Any(ta => ta.FullName == referencedAssembly.FullName ||
                                              ta.GetName().Name == referencedAssembly.Name))
                {
                    // Additional check: verify it's actual usage, not just a reference
                    // by checking if types in source use types from target
                    if (HasActualTypeUsage(sourceAssembly, sourceNamespacePrefix, targetNamespacePrefix))
                    {
                        return true;
                    }
                }
            }
        }

        // Fallback: check type-level dependencies for cases where assemblies might be loaded differently
        return HasTypeLevelDependency(sourceNamespacePrefix, targetNamespacePrefix);
    }

    private static bool ContainsTypesInNamespace(Assembly assembly, string namespacePrefix)
    {
        try
        {
            return assembly.GetTypes().Any(t => t.Namespace?.StartsWith(namespacePrefix) == true);
        }
        catch (ReflectionTypeLoadException)
        {
            return false;
        }
    }

    private static bool HasActualTypeUsage(Assembly sourceAssembly, string sourceNamespacePrefix, string targetNamespacePrefix)
    {
        try
        {
            Type[] sourceTypes = sourceAssembly.GetTypes()
                .Where(t => t.Namespace?.StartsWith(sourceNamespacePrefix) == true)
                .ToArray();

            foreach (Type type in sourceTypes)
            {
                // Skip compiler-generated types
                if (IsCompilerGenerated(type))
                    continue;

                // Check constructor parameters
                foreach (ConstructorInfo ctor in type.GetConstructors())
                {
                    foreach (ParameterInfo param in ctor.GetParameters())
                    {
                        if (IsTypeInNamespace(param.ParameterType, targetNamespacePrefix))
                        {
                            return true;
                        }
                    }
                }

                // Check fields (only non-compiler generated)
                foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => !IsCompilerGenerated(f)))
                {
                    if (IsTypeInNamespace(field.FieldType, targetNamespacePrefix))
                    {
                        return true;
                    }
                }

                // Check method return types and parameters (skip property accessors)
                foreach (MethodInfo method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => !m.IsSpecialName && !IsCompilerGenerated(m)))
                {
                    if (IsTypeInNamespace(method.ReturnType, targetNamespacePrefix))
                    {
                        return true;
                    }

                    foreach (ParameterInfo param in method.GetParameters())
                    {
                        if (IsTypeInNamespace(param.ParameterType, targetNamespacePrefix))
                        {
                            return true;
                        }
                    }
                }

                // Check interfaces
                foreach (Type iface in type.GetInterfaces())
                {
                    if (iface.Namespace?.StartsWith(targetNamespacePrefix) == true)
                    {
                        return true;
                    }
                }

                // Check base type
                if (type.BaseType?.Namespace?.StartsWith(targetNamespacePrefix) == true)
                {
                    return true;
                }
            }
        }
        catch (ReflectionTypeLoadException)
        {
            // Skip assemblies that can't be loaded
        }

        return false;
    }

    private static bool IsCompilerGenerated(MemberInfo member)
    {
        return member.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) ||
               member.Name.Contains('<') ||
               member.Name.Contains('>') ||
               member.Name.Contains("__") ||
               member.Name.StartsWith("<>", StringComparison.Ordinal);
    }

    /// <summary>
    /// Fallback method to check type-level dependencies when assembly references don't reveal the dependency.
    /// </summary>
    private static bool HasTypeLevelDependency(string sourceNamespacePrefix, string targetNamespacePrefix)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Ghost.") == true)
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException) { return Array.Empty<Type>(); }
            })
            .Where(t => t.Namespace?.StartsWith(sourceNamespacePrefix) == true)
            .Where(t => !IsCompilerGenerated(t))
            .Any(t => HasTypeDependencyOn(t, targetNamespacePrefix));
    }

    /// <summary>
    /// Gets all types that have dependencies on the specified namespace.
    /// </summary>
    public static IEnumerable<string> GetTypesWithDependencyOn(string targetNamespacePrefix)
    {
        List<string> violatingTypes = new();
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Ghost.") == true)
            .ToArray();

        foreach (Assembly assembly in assemblies)
        {
            try
            {
                Type[] types = assembly.GetTypes();

                foreach (Type type in types)
                {
                    if (HasTypeDependencyOn(type, targetNamespacePrefix))
                    {
                        violatingTypes.Add(type.FullName ?? type.Name);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }
        }

        return violatingTypes;
    }

    private static bool HasTypeDependencyOn(Type type, string targetNamespacePrefix)
    {
        // Check constructor parameters
        foreach (ConstructorInfo ctor in type.GetConstructors())
        {
            foreach (ParameterInfo param in ctor.GetParameters())
            {
                if (IsTypeInNamespace(param.ParameterType, targetNamespacePrefix))
                {
                    return true;
                }
            }
        }

        // Check fields
        foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
        {
            if (IsTypeInNamespace(field.FieldType, targetNamespacePrefix))
            {
                return true;
            }
        }

        // Check properties
        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
        {
            if (IsTypeInNamespace(prop.PropertyType, targetNamespacePrefix))
            {
                return true;
            }
        }

        // Check method return types and parameters
        foreach (MethodInfo method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => !m.IsSpecialName))
        {
            if (IsTypeInNamespace(method.ReturnType, targetNamespacePrefix))
            {
                return true;
            }

            foreach (ParameterInfo param in method.GetParameters())
            {
                if (IsTypeInNamespace(param.ParameterType, targetNamespacePrefix))
                {
                    return true;
                }
            }
        }

        // Check interfaces
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.Namespace?.StartsWith(targetNamespacePrefix) == true)
            {
                return true;
            }
        }

        // Check base type
        if (type.BaseType?.Namespace?.StartsWith(targetNamespacePrefix) == true)
        {
            return true;
        }

        return false;
    }

    private static bool IsTypeInNamespace(Type? type, string namespacePrefix)
    {
        if (type == null)
        {
            return false;
        }

        // Unwrap nullable types
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Check the type itself
        if (underlyingType.Namespace?.StartsWith(namespacePrefix) == true)
        {
            return true;
        }

        // Check generic type arguments
        if (underlyingType.IsGenericType)
        {
            foreach (Type arg in underlyingType.GetGenericArguments())
            {
                if (IsTypeInNamespace(arg, namespacePrefix))
                {
                    return true;
                }
            }
        }

        // Check element type for arrays
        if (underlyingType.IsArray)
        {
            return IsTypeInNamespace(underlyingType.GetElementType(), namespacePrefix);
        }

        return false;
    }

    /// <summary>
    /// Gets types in the global namespace (no namespace declared).
    /// </summary>
    public static IEnumerable<Type> GetTypesInGlobalNamespace(params Assembly[] assemblies)
    {
        List<Type> globalTypes = new();

        foreach (Assembly assembly in assemblies)
        {
            try
            {
                Type[] types = assembly.GetTypes()
                    .Where(t => string.IsNullOrEmpty(t.Namespace))
                    .ToArray();
                globalTypes.AddRange(types);
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }
        }

        return globalTypes;
    }

    /// <summary>
    /// Gets all assemblies that reference the specified assembly name.
    /// </summary>
    public static IEnumerable<Assembly> GetAssembliesReferencing(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a => a.GetReferencedAssemblies().Any(r => r.Name == assemblyName));
    }
}
