namespace H.IpcGenerators;

/// <summary>
/// 
/// </summary>
[MessagePack.MessagePackObject]
public class RunMethodRequest : RpcRequest
{
    /// <summary>
    /// 
    /// </summary>
    [MessagePack.Key(2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Serialized method arguments.
    /// </summary>
    [MessagePack.Key(3)]
    public string[] Arguments { get; set; } = [];

    /// <summary>
    /// 
    /// </summary>
    public RunMethodRequest() : base(RpcRequestType.RunMethod)
    {
    }
}
