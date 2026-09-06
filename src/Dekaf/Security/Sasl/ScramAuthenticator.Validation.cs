using Dekaf.Errors;

namespace Dekaf.Security.Sasl;

public sealed partial class ScramAuthenticator
{
    private static ParsedMessage ParseMessage(ReadOnlySpan<char> message, bool serverFirst)
    {
        var result = new ParsedMessage();
        ulong seen = 0;
        var attributeIndex = 0;
        while (!message.IsEmpty)
        {
            var separator = message.IndexOf(',');
            var part = separator < 0 ? message : message[..separator];
            if (part.Length < 2 || part[1] != '=')
                throw new AuthenticationException("Malformed SCRAM attribute");

            var name = part[0];
            var bit = name switch
            {
                >= 'A' and <= 'Z' => name - 'A',
                >= 'a' and <= 'z' => name - 'a' + 26,
                _ => -1
            };
            if (bit < 0)
                throw new AuthenticationException("Invalid SCRAM attribute name");
            if (name == 'm')
                throw new AuthenticationException("Unsupported mandatory SCRAM extension");
            var mask = 1UL << bit;
            if ((seen & mask) != 0)
                throw new AuthenticationException("Duplicate SCRAM attribute");
            seen |= mask;

            var value = part[2..];
            var allowsEmptyBase64 = serverFirst
                ? attributeIndex == 1 && name == 's'
                : attributeIndex == 0 && name == 'v';
            if (value.IndexOf('\0') >= 0 || (value.IsEmpty && !allowsEmptyBase64))
                throw new AuthenticationException("Invalid SCRAM attribute value");
            if (serverFirst && attributeIndex < 3 && name != "rsi"[attributeIndex])
                throw new AuthenticationException("SCRAM server-first-message requires nonce, salt, and iterations in order");
            if (!serverFirst && attributeIndex == 0 && name is not ('v' or 'e'))
                throw new AuthenticationException("SCRAM server-final-message requires a verifier or error");
            if (!serverFirst && attributeIndex > 0 && name is 'v' or 'e')
                throw new AuthenticationException("SCRAM server-final-message cannot contain both verifier and error");

            switch (name)
            {
                case 'r': result.Nonce = value; break;
                case 's': result.Salt = value; break;
                case 'i': result.Iterations = value; break;
                case 'v': result.Verifier = value; break;
                case 'e': result.Error = value; break;
            }
            attributeIndex++;
            if (separator < 0)
                break;
            message = message[(separator + 1)..];
            if (message.IsEmpty)
                throw new AuthenticationException("Trailing separator in SCRAM server message");
        }

        if (attributeIndex < (serverFirst ? 3 : 1))
            throw new AuthenticationException("Required SCRAM server attributes are missing");
        return result;
    }

    private int ParseIterationCount(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value[0] is < '1' or > '9')
            throw new AuthenticationException("SCRAM iteration count must be a positive decimal integer");
        var count = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var digit = value[index] - '0';
            if ((uint)digit > 9 || count > (int.MaxValue - digit) / 10)
                throw new AuthenticationException("Invalid or out-of-range SCRAM iteration count");
            count = count * 10 + digit;
        }
        if (count > _maxIterations)
            throw new AuthenticationException($"SCRAM iteration count {count} exceeds configured maximum {_maxIterations} (SaslScramMaxIterations)");
        return count;
    }

    private static byte[] DecodeBase64(ReadOnlySpan<char> value, string error)
    {
        if ((value.Length & 3) != 0)
            throw new AuthenticationException(error);
        if (value.IsEmpty)
            return Array.Empty<byte>();

        var padding = 0;
        if (value[^1] == '=')
            padding = value[^2] == '=' ? 2 : 1;
        var payloadLength = value.Length - padding;
        var last = 0;
        for (var index = 0; index < payloadLength; index++)
        {
            last = Base64Value(value[index]);
            if (last < 0)
                throw new AuthenticationException(error);
        }
        // RFC 5802 requires canonical base64, including zero unused padding bits.
        if ((padding == 2 && (last & 15) != 0) || (padding == 1 && (last & 3) != 0))
            throw new AuthenticationException(error);
        return Convert.FromBase64String(value.ToString());
    }

    private static int Base64Value(char value) => value switch
    {
        >= 'A' and <= 'Z' => value - 'A',
        >= 'a' and <= 'z' => value - 'a' + 26,
        >= '0' and <= '9' => value - '0' + 52,
        '+' => 62,
        '/' => 63,
        _ => -1
    };

    private static string GetServerError(ReadOnlySpan<char> error) => error switch
    {
        "invalid-encoding" => "invalid-encoding",
        "extensions-not-supported" => "extensions-not-supported",
        "invalid-proof" => "invalid-proof",
        "channel-bindings-dont-match" => "channel-bindings-dont-match",
        "server-does-support-channel-binding" => "server-does-support-channel-binding",
        "channel-binding-not-supported" => "channel-binding-not-supported",
        "unsupported-channel-binding-type" => "unsupported-channel-binding-type",
        "unknown-user" => "unknown-user",
        "invalid-username-encoding" => "invalid-username-encoding",
        "no-resources" => "no-resources",
        _ => "other-error"
    };

    private ref struct ParsedMessage
    {
        public ReadOnlySpan<char> Nonce;
        public ReadOnlySpan<char> Salt;
        public ReadOnlySpan<char> Iterations;
        public ReadOnlySpan<char> Verifier;
        public ReadOnlySpan<char> Error;
    }
}
