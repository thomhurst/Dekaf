global using KafkaException = Dekaf.Errors.KafkaException;

#if NETSTANDARD2_0
global using System.Text;
global using ArgumentException = Dekaf.Compatibility.ArgumentExceptionCompat;
global using ArgumentNullException = Dekaf.Compatibility.ArgumentNullExceptionCompat;
global using ArgumentOutOfRangeException = Dekaf.Compatibility.ArgumentOutOfRangeExceptionCompat;
global using Array = Dekaf.Compatibility.ArrayCompat;
global using BinaryPrimitives = Dekaf.Compatibility.BinaryPrimitivesCompat;
global using BitOperations = Dekaf.Compatibility.BitOperationsCompat;
global using Convert = Dekaf.Compatibility.ConvertCompat;
global using Dns = Dekaf.Compatibility.DnsCompat;
global using File = Dekaf.Compatibility.FileCompat;
global using GC = Dekaf.Compatibility.GCCompat;
global using HMACSHA256 = Dekaf.Compatibility.HMACSHA256Compat;
global using HMACSHA512 = Dekaf.Compatibility.HMACSHA512Compat;
global using Math = Dekaf.Compatibility.MathCompat;
global using OperatingSystem = Dekaf.Compatibility.OperatingSystemCompat;
global using Random = Dekaf.Compatibility.RandomCompat;
global using Rfc2898DeriveBytes = Dekaf.Compatibility.Rfc2898DeriveBytesCompat;
global using SHA256 = Dekaf.Compatibility.SHA256Compat;
global using SHA512 = Dekaf.Compatibility.SHA512Compat;
global using Stopwatch = Dekaf.Compatibility.StopwatchCompat;
global using Thread = Dekaf.Compatibility.ThreadCompat;
#endif
