namespace H.IpcGenerators;

/// <summary>
/// 
/// </summary>
[MessagePack.MessagePackObject]
public class RpcRequest
{
    /// <summary>
    /// 
    /// </summary>
    [MessagePack.Key(0)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 
    /// </summary>
    [MessagePack.Key(1)]
    public RpcRequestType Type { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public RpcRequest()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    public RpcRequest(RpcRequestType type)
    {
        Type = type;
    }
}
