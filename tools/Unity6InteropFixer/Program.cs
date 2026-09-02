using dnlib.DotNet;
using dnlib.DotNet.Writer;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: BetterUI.Unity6InteropFixer <BepInEx-interop-directory>");
    return 1;
}

string interopDirectory = Path.GetFullPath(args[0]);
string[] affectedAssemblies =
{
    "UnityEngine.CoreModule.dll",
    "Unity.Collections.dll"
};

int totalRemoved = 0;
foreach (string assemblyName in affectedAssemblies)
{
    string assemblyPath = Path.Combine(interopDirectory, assemblyName);
    if (!File.Exists(assemblyPath))
    {
        Console.WriteLine($"Skipped missing {assemblyName}");
        continue;
    }

    int removed = RepairAssembly(assemblyPath);
    totalRemoved += removed;
    Console.WriteLine(removed == 0
        ? $"Clean: {assemblyName}"
        : $"Repaired: {assemblyName} ({removed} duplicate helper types removed)");
}

Console.WriteLine($"Unity 6 interop repair complete; removed {totalRemoved} duplicates.");
return 0;

static int RepairAssembly(string assemblyPath)
{
    byte[] input = File.ReadAllBytes(assemblyPath);
    using ModuleDefMD module = ModuleDefMD.Load(input, new ModuleCreationOptions
    {
        TryToLoadPdbFromDisk = false
    });

    int removed = RemoveDuplicateTopLevelTypes(module);
    foreach (TypeDef type in module.GetTypes().ToList())
    {
        removed += RemoveDuplicateNestedTypes(type);
    }

    if (removed == 0)
    {
        return 0;
    }

    string backupPath = assemblyPath + ".betterui-backup";
    if (!File.Exists(backupPath))
    {
        File.Copy(assemblyPath, backupPath);
    }

    string temporaryPath = assemblyPath + ".betterui-tmp";
    var options = new ModuleWriterOptions(module)
    {
        Logger = DummyLogger.NoThrowInstance
    };
    module.Write(temporaryPath, options);
    File.Move(temporaryPath, assemblyPath, true);
    return removed;
}

static int RemoveDuplicateTopLevelTypes(ModuleDefMD module)
{
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var duplicates = new List<TypeDef>();

    foreach (TypeDef type in module.Types)
    {
        if (!seen.Add(type.FullName))
        {
            duplicates.Add(type);
        }
    }

    foreach (TypeDef duplicate in duplicates)
    {
        module.Types.Remove(duplicate);
    }

    return duplicates.Count;
}
static int RemoveDuplicateNestedTypes(TypeDef type)
{
    if (type.NestedTypes.Count < 2)
    {
        return 0;
    }

    var seen = new HashSet<string>(StringComparer.Ordinal);
    var duplicates = new List<TypeDef>();

    foreach (TypeDef nested in type.NestedTypes)
    {
        if (!seen.Add(nested.Name.String))
        {
            duplicates.Add(nested);
        }
    }

    foreach (TypeDef duplicate in duplicates)
    {
        type.NestedTypes.Remove(duplicate);
    }

    return duplicates.Count;
}
