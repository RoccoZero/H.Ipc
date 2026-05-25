using H.Generators.Tests.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace H.Generators.IntegrationTests;

public static class TestHelper
{
    public static async Task CheckSourceAsync(
        this VerifyBase verifier,
        string source,
        CancellationToken cancellationToken = default)
    {
        var referenceAssemblies = LatestReferenceAssemblies.Net80
            .WithPackages([new PackageIdentity("H.Pipes.AccessControl", "2.0.59")]);
        var references = await referenceAssemblies.ResolveAsync(null, cancellationToken);
        var compilation = (Compilation)CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[]
            {
                CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken),
                CSharpSyntaxTree.ParseText(IpcGeneratorsStubs, cancellationToken: cancellationToken),
            },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver
            .Create(new HIpcGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out compilation, out _, cancellationToken);
        var diagnostics = compilation.GetDiagnostics(cancellationToken);

        await Task.WhenAll(
            verifier
                .Verify(diagnostics)
                .UseDirectory("Snapshots")
                .UseTextForParameters("Diagnostics"),
            verifier
                .Verify(driver)
                .UseDirectory("Snapshots"));
    }

    private const string IpcGeneratorsStubs = """
#nullable enable

namespace H.IpcGenerators;

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class IpcClientAttribute : System.Attribute;

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class IpcServerAttribute : System.Attribute;

public static class IpcSerializer
{
    public static string Serialize<T>(T value)
    {
        return string.Empty;
    }

    public static T? Deserialize<T>(string value)
    {
        return default;
    }
}

public enum RpcRequestType
{
    RunMethod,
    ReturnMethodResult,
}

public class RpcRequest
{
    public System.Guid Id { get; set; } = System.Guid.NewGuid();
    public RpcRequestType Type { get; set; }

    public RpcRequest(RpcRequestType type)
    {
        Type = type;
    }
}

public class RunMethodRequest : RpcRequest
{
    public string Name { get; set; } = string.Empty;
    public string[] Arguments { get; set; } = [];

    public RunMethodRequest() : base(RpcRequestType.RunMethod)
    {
    }
}

public class ReturnMethodResultRequest : RpcRequest
{
    public ReturnMethodResultRequest() : base(RpcRequestType.ReturnMethodResult)
    {
        IsSuccessful = false;
        ErrMsg = string.Empty;
        ResultType = string.Empty;
    }

    public ReturnMethodResultRequest(bool isSuccessful, string? errorMessage) : this()
    {
        IsSuccessful = isSuccessful;
        ErrMsg = errorMessage;
    }

    protected ReturnMethodResultRequest(string resultType) : base(RpcRequestType.ReturnMethodResult)
    {
        ResultType = resultType;
        IsSuccessful = true;
        ErrMsg = string.Empty;
    }

    public string ResultType { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrMsg { get; set; }
    public string? ResultPayload { get; set; }
}

public class ReturnMethodResultRequest<T> : ReturnMethodResultRequest
{
    public ReturnMethodResultRequest()
    {
        Result = default;
    }

    public ReturnMethodResultRequest(T result) : base(typeof(T).Name)
    {
        Result = result;
    }

    public T? Result { get; set; }
}

public static class ReturnMethodResultFactory
{
    public static ReturnMethodResultRequest<T> Create<T>(T result)
    {
        return new ReturnMethodResultRequest<T>(result);
    }
}
""";
}
