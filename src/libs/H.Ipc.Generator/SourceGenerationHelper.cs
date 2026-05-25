using System.Data;
using H.Generators.Extensions;
using Microsoft.CodeAnalysis;

namespace H.Generators;

internal static class SourceGenerationHelper
{
    public static string GenerateClientImplementation(ClassData @class)
    {
        return @$"
#nullable enable

namespace {@class.Namespace}
{{
    public partial class {@class.Name}
    {{
        private bool isWaitingForServerResponse = false;

        #region Properties

        private global::H.Pipes.IPipeConnection<string>? _connection;
        private global::H.Pipes.IPipeConnection<string> Connection
        {{
            get
            {{
                return _connection ?? throw new global::System.InvalidOperationException(""You need to call Initialize() first.""); 
            }}
        }}

        #endregion

        #region Events

{GenerateExceptionOccurredEvent()}

        #endregion

{GenerateThrowIfWaitingMethod()}

        public void Initialize(global::H.Pipes.IPipeConnection<string>? connection)
        {{
            _connection = connection ?? throw new global::System.ArgumentNullException(nameof(connection));
        }}

{@class.Methods.Select(static method => GenerateClientMethod(method)).Inject()}

        private async global::System.Threading.Tasks.Task WriteAsync(
            global::H.IpcGenerators.RunMethodRequest method,
            global::System.Threading.CancellationToken cancellationToken = default)
        {{
            var payload = global::H.IpcGenerators.IpcSerializer.Serialize(method);
            await Connection.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }}

        private static global::H.IpcGenerators.RunMethodRequest CreateRunMethodRequest(
            string name,
            params string[] arguments)
        {{
            var request = new global::H.IpcGenerators.RunMethodRequest
            {{
                Name = name,
                Arguments = arguments,
            }};

            return request;
        }}
    }}
}}";
    }

    private static string GenerateThrowIfWaitingMethod()
    {
        return @"
        private void ThrowIfWaitingForServer()
        {
            if (isWaitingForServerResponse)
            {
                throw new global::System.InvalidOperationException(""Cannot perform this operation while waiting for server response."");
            }
        }
";
    }

    private static string GenerateClientMethod(IMethodSymbol method)
    {
        var isTask = method.ReturnType.Name == nameof(Task);
        var isInSystemNameSpace = method.ReturnType.ContainingNamespace.ToDisplayString() == typeof(Task).Namespace;
        if (!(isTask && isInSystemNameSpace))
        {
            throw new InvalidExpressionException(
                $"Method '{method.Name}' in interface '{method.ContainingType.Name}' must declare return type 'Task' or 'Task<T>'. Is Task: {isTask}  Is in System namespace: {isInSystemNameSpace}");
        }

        if (method.ReturnType is INamedTypeSymbol { Arity: 1 } namedTypeSymbol)
        {
            var coreReturnType = namedTypeSymbol.TypeArguments.First();
            var coreReturnTypeFullName = GetFullyQualifiedTypeName(coreReturnType);

            return $@"
        public async {method.ReturnType} {method.Name}({string.Join(", ", method.Parameters.Select(static parameter => $"{parameter.Type} {parameter.Name}"))})
        {{
            ThrowIfWaitingForServer();

            var tcs = new global::System.Threading.Tasks.TaskCompletionSource<{coreReturnTypeFullName}>();

            void ReceiveResult(object? sender, H.Pipes.Args.ConnectionMessageEventArgs<string?> e)
            {{
                var payload = e.Message ?? throw new global::System.ArgumentException(""Message property of received H.Pipes.Args.ConnectionMessageEventArgs<string> object is null"");
                var result = default({coreReturnTypeFullName});
                var resultGeneral = global::H.IpcGenerators.IpcSerializer.Deserialize<global::H.IpcGenerators.ReturnMethodResultRequest>(payload);
                if (resultGeneral?.ResultType == ""{coreReturnType.Name}"")
                {{
                    if (string.IsNullOrWhiteSpace(resultGeneral.ResultPayload))
                    {{
                        throw new global::System.InvalidOperationException(""ResultPayload is empty."");
                    }}

                    result = global::H.IpcGenerators.IpcSerializer.Deserialize<{coreReturnTypeFullName}>(resultGeneral.ResultPayload);
                }}

                Connection.MessageReceived -= ReceiveResult;
                {GenerateTaskCompletionAssignment(coreReturnType)}
            }}

            isWaitingForServerResponse = true;
            try
            {{
                Connection.MessageReceived += ReceiveResult;
                await WriteAsync(CreateRunMethodRequest(nameof({method.Name}){GenerateClientArguments(method)})).ConfigureAwait(false);

                var result = await tcs.Task;
                isWaitingForServerResponse = false;
                return result;
            }}
            catch (global::System.Exception exception)
            {{
                isWaitingForServerResponse = false;
                OnExceptionOccurred(exception);
                return await global::System.Threading.Tasks.Task.FromException<{coreReturnType}>(exception);
            }}
        }}
";
        }

        return $@"
        public async {method.ReturnType} {method.Name}({string.Join(", ", method.Parameters.Select(static parameter => $"{parameter.Type} {parameter.Name}"))})
        {{
            ThrowIfWaitingForServer();
            try
            {{
                await WriteAsync(CreateRunMethodRequest(nameof({method.Name}){GenerateClientArguments(method)})).ConfigureAwait(false);
            }}
            catch (global::System.Exception exception)
            {{
                OnExceptionOccurred(exception);
            }}
        }}
";
    }

    private static string GenerateClientArguments(IMethodSymbol method)
    {
        if (method.Parameters.Length == 0)
        {
            return string.Empty;
        }

        return ", " + string.Join(", ", method.Parameters.Select(static parameter => $"global::H.IpcGenerators.IpcSerializer.Serialize({parameter.Name})"));
    }

    private static string GenerateTaskCompletionAssignment(ITypeSymbol coreReturnType)
    {
        if (coreReturnType.IsReferenceType)
        {
            return $@"
                if (result is null)
                {{
                    tcs.SetException(new global::System.InvalidOperationException(""Failed to deserialize MessagePack result""));
                }}
                else
                {{
                    tcs.SetResult(result);
                }}
";
        }

        return "tcs.SetResult(result);";
    }

    public static string GenerateServerImplementation(ClassData @class)
    {
        return @$"
#nullable enable

namespace {@class.Namespace}
{{
    public partial class {@class.Name}
    {{
        #region Events

{GenerateExceptionOccurredEvent()}

        #endregion

        public void Initialize(global::H.Pipes.IPipeConnection<string> connection)
        {{
            connection = connection ?? throw new global::System.ArgumentNullException(nameof(connection));
            connection.MessageReceived += async (_, args) =>
            {{
                try
                {{
                    var payload = args.Message ?? throw new global::System.InvalidOperationException(""Message is null."");
                    var request = Deserialize<global::H.IpcGenerators.RpcRequest>(payload);

                    if (request.Type == global::H.IpcGenerators.RpcRequestType.RunMethod)
                    {{
                        var method = Deserialize<global::H.IpcGenerators.RunMethodRequest>(payload);
                        switch (method.Name)
                        {{
{@class.Methods.Select(static method => GenerateServerUnpackMethod(method)).Inject()}
                        }}
                    }}
                }}
                catch (global::System.Exception exception)
                {{
                    OnExceptionOccurred(exception);
                    var result = new global::H.IpcGenerators.ReturnMethodResultRequest(false, exception.Message);
                    var payload = Serialize(result);
                    await connection.WriteAsync(payload).ConfigureAwait(false);
                }}
            }};
        }}

        private static T Deserialize<T>(string payload)
        {{
            return
                global::H.IpcGenerators.IpcSerializer.Deserialize<T>(payload) ??
                throw new global::System.ArgumentException($@""Returned null when trying to deserialize to {{typeof(T)}}.
    payload:
    {{payload}}"");
        }}

        private static string Serialize<T>(T obj)
        {{
            return global::H.IpcGenerators.IpcSerializer.Serialize(obj);
        }}
    }}
}}";
    }

    private static string GenerateServerUnpackMethod(IMethodSymbol method)
    {
        var isTask = method.ReturnType.Name == nameof(Task);
        var isInSystemNameSpace = method.ReturnType.ContainingNamespace.ToDisplayString() == typeof(Task).Namespace;
        if (!(isTask && isInSystemNameSpace))
        {
            throw new InvalidExpressionException(
                $"Method '{method.Name}' in interface '{method.ContainingType.Name}' must declare return type 'Task' or 'Task<T>'.");
        }

        if (method.ReturnType is INamedTypeSymbol { Arity: 1 })
        {
            return $@"
                            case nameof({method.Name}):
                            {{
{GenerateServerArguments(method)}
                                var resultCore = await {method.Name}({string.Join(", ", method.Parameters.Select(static parameter => parameter.Name)).TrimEnd(',', ' ', '\n', '\r')});
                                var result = global::H.IpcGenerators.ReturnMethodResultFactory.Create(resultCore);
                                result.ResultPayload = Serialize(resultCore);
                                var responsePayload = Serialize<global::H.IpcGenerators.ReturnMethodResultRequest>(result);
                                await connection.WriteAsync(responsePayload).ConfigureAwait(false);
                                break;
                            }}";
        }

        return $@"
                            case nameof({method.Name}):
                            {{
{GenerateServerArguments(method)}
                                await {method.Name}({string.Join(", ", method.Parameters.Select(static parameter => parameter.Name)).TrimEnd(',', ' ', '\n', '\r')});
                                break;
                            }}";
    }

    private static string GenerateServerArguments(IMethodSymbol method)
    {
        return method.Parameters
            .Select(static (parameter, index) => $@"                                var {parameter.Name} = Deserialize<{GetFullyQualifiedTypeName(parameter.Type)}>(method.Arguments[{index}]);")
            .Inject();
    }

    public static string GenerateRequests(ClassData @class, bool server)
    {
        return @$"
#nullable enable

namespace {@class.Namespace}
{{
{@class.Methods.Select(method => $@"
    public class {method.Name}{(server ? "Server" : "Client")}Method : global::H.IpcGenerators.RunMethodRequest
    {{
{method.Parameters.Select(static parameter => $@"
        public {parameter.Type} {parameter.Name.ToPropertyName()} {{ get; set; }}
").Inject()}
 
        public {method.Name}{(server ? "Server" : "Client")}Method({string.Join(", ", method.Parameters.Select(static parameter => $"{parameter.Type} {parameter.Name}"))})
        {{
            Name = ""{method.Name}"";
{method.Parameters.Select(static parameter => $@" 
            {parameter.Name.ToPropertyName()} = {parameter.Name} ?? throw new global::System.ArgumentNullException(nameof({parameter.Name}));
 ").Inject()}
        }}
    }}
").Inject()}
}}".RemoveBlankLinesWhereOnlyWhitespaces();
    }

    public static string GenerateExceptionOccurredEvent()
    {
        return @" 
        public event global::System.EventHandler<global::System.Exception>? ExceptionOccurred;

        private void OnExceptionOccurred(global::System.Exception exception)
        {
            ExceptionOccurred?.Invoke(this, exception);
        }
 ".RemoveBlankLinesWhereOnlyWhitespaces();
    }

    private static string GetFullyQualifiedTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
