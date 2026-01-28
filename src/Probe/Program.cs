using System;
using System.Linq;
using System.Reflection;
using Patchright;

Console.WriteLine("Inspecting Patchright Types...");

void PrintType(Type t)
{
    Console.WriteLine($"\n--- {t.Name} ---");
    foreach (var p in t.GetProperties())
    {
        Console.WriteLine($"  Prop: {p.Name} ({p.PropertyType.Name})");
    }
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        if (!m.IsSpecialName) Console.WriteLine($"  Method: {m.Name}");
    }
}

try
{
    PrintType(typeof(LaunchOptions));
    PrintType(typeof(BrowserNewContextOptions));
    PrintType(typeof(IBrowserContext));
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}



