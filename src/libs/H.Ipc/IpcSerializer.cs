using MessagePack;

namespace H.IpcGenerators;

/// <summary>
/// MessagePack IPC serializer used by generated clients and servers.
/// </summary>
public static class IpcSerializer
{
    /// <summary>
    /// Serializes a value to the current string transport.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>Base64-encoded MessagePack payload.</returns>
    public static string Serialize<T>(T value)
    {
        return Convert.ToBase64String(MessagePackSerializer.Serialize(value));
    }

    /// <summary>
    /// Deserializes a value from the current string transport.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">Base64-encoded MessagePack payload.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(string value)
    {
        return MessagePackSerializer.Deserialize<T>(Convert.FromBase64String(value));
    }
}
