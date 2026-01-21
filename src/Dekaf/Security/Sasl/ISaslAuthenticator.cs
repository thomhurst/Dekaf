namespace Dekaf.Security.Sasl;

/// <summary>
/// Interface for SASL authentication mechanism implementations.
/// </summary>
public interface ISaslAuthenticator
{
    /// <summary>
    /// Gets the mechanism name as used in the Kafka protocol.
    /// </summary>
    string MechanismName { get; }

    /// <summary>
    /// Gets the initial authentication bytes to send to the server.
    /// </summary>
    byte[] GetInitialResponse();

    /// <summary>
    /// Evaluates a challenge from the server and returns the response.
    /// </summary>
    /// <param name="challenge">The challenge bytes from the server.</param>
    /// <returns>The response bytes to send back, or null if authentication is complete.</returns>
    byte[]? EvaluateChallenge(byte[] challenge);

    /// <summary>
    /// Returns true if authentication is complete.
    /// </summary>
    bool IsComplete { get; }
}
