global using KafkaException = Dekaf.Errors.KafkaException;

#if NETSTANDARD2_0
global using Polyfills;
global using Array = Dekaf.NetStandard.ArrayCompat;
global using Ascii = Dekaf.NetStandard.AsciiCompat;
global using BitOperations = Dekaf.NetStandard.BitOperationsCompat;
global using Dns = Dekaf.NetStandard.DnsCompat;
global using GC = Dekaf.NetStandard.GCCompat;
global using HashCode = Dekaf.NetStandard.HashCodeCompat;
global using HMACSHA256 = Dekaf.NetStandard.HMACSHA256Compat;
global using HMACSHA512 = Dekaf.NetStandard.HMACSHA512Compat;
global using Rfc2898DeriveBytes = Dekaf.NetStandard.Rfc2898DeriveBytesCompat;
global using Thread = Dekaf.NetStandard.ThreadCompat;
#endif
