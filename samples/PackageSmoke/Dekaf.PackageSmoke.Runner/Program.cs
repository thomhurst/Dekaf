using Dekaf.PackageSmoke.NetStandard20;

var result = NetStandardPackageSmoke.Run();

if (!result.StartsWith("dekaf:", StringComparison.Ordinal))
{
    throw new InvalidOperationException($"Unexpected package smoke result: {result}");
}

Console.WriteLine(result);
