using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Worker.Interceptors;

namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Client and worker interceptor that encrypts <see cref="Secret{T}"/> values on
/// the way out (client) and decrypts them on the way in (activity), so secrets
/// carried opaquely through a workflow are never visible in plaintext to the
/// Temporal service or UI. The key material is resolved once from an
/// <see cref="ISecretResolver"/> (Azure Key Vault / AWS Secrets Manager).
/// </summary>
/// <remarks>
/// This complements — and is independent of — the whole-payload
/// <see cref="EncryptionCodec"/>: when both are in play the secret is encrypted
/// twice (once here, once by the codec).
/// </remarks>
public sealed class SecretEncryptionInterceptor : IClientInterceptor, IWorkerInterceptor
{
    private readonly ISecretResolver resolver;
    private readonly string secretId;
    private readonly string keyId;
    private readonly string encoding;
    private byte[]? cachedKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretEncryptionInterceptor"/> class.
    /// </summary>
    /// <param name="resolver">The secret resolver to fetch the encryption key from.</param>
    /// <param name="secretId">The secret name/id holding the AES-GCM key.</param>
    /// <param name="keyId">The key id stamped onto encrypted secrets. Defaults to <c>"default"</c>.</param>
    /// <param name="encoding">How the secret decodes into key bytes: <c>raw</c>, <c>base64</c>, or <c>hex</c>. Defaults to <c>raw</c>.</param>
    public SecretEncryptionInterceptor(ISecretResolver resolver, string secretId, string keyId = "default", string encoding = "raw")
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrEmpty(secretId);
        this.resolver = resolver;
        this.secretId = secretId;
        this.keyId = keyId;
        this.encoding = encoding;
    }

    /// <summary>Encrypts every <see cref="ISecret"/> in the argument graph in place.</summary>
    public async Task EncryptAsync(IEnumerable<object?> args, CancellationToken cancellationToken = default)
    {
        var key = await GetKeyAsync(cancellationToken).ConfigureAwait(false);
        await SecretGraphWalker.WalkAsync(args, secret => secret.EncryptAsync(key, keyId)).ConfigureAwait(false);
    }

    /// <summary>Decrypts every <see cref="ISecret"/> in the argument graph in place.</summary>
    public async Task DecryptAsync(IEnumerable<object?> args, CancellationToken cancellationToken = default)
    {
        var key = await GetKeyAsync(cancellationToken).ConfigureAwait(false);
        await SecretGraphWalker.WalkAsync(args, secret =>
        {
            if (secret.IsEncrypted && secret.KeyId != keyId)
            {
                throw new InvalidOperationException(
                    $"Secret was encrypted with key id '{secret.KeyId}', expected '{keyId}'.");
            }

            return secret.DecryptAsync(key);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next) =>
        new Outbound(this, next);

    /// <inheritdoc />
    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor next) => next;

    /// <inheritdoc />
    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor next) =>
        new ActivityInbound(this, next);

    private async Task<byte[]> GetKeyAsync(CancellationToken cancellationToken)
    {
        if (cachedKey is not null)
        {
            return cachedKey;
        }

        var secret = await resolver.ResolveAsync(secretId, cancellationToken).ConfigureAwait(false);
        cachedKey = DecodeKey(secret, encoding);
        return cachedKey;
    }

    private static byte[] DecodeKey(string secret, string encoding) =>
        encoding switch
        {
            "raw" => System.Text.Encoding.ASCII.GetBytes(secret),
            "base64" => Convert.FromBase64String(secret),
            "hex" => Convert.FromHexString(secret),
            _ => throw new InvalidOperationException(
                $"Unknown key encoding '{encoding}'. Expected 'raw', 'base64', or 'hex'."),
        };

    private sealed class Outbound : ClientOutboundInterceptor
    {
        private readonly SecretEncryptionInterceptor root;

        public Outbound(SecretEncryptionInterceptor root, ClientOutboundInterceptor next)
            : base(next) => this.root = root;

        public override async Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input)
        {
            await root.EncryptAsync(input.Args).ConfigureAwait(false);
            return await base.StartWorkflowAsync<TWorkflow, TResult>(input).ConfigureAwait(false);
        }

        public override async Task SignalWorkflowAsync(SignalWorkflowInput input)
        {
            await root.EncryptAsync(input.Args).ConfigureAwait(false);
            await base.SignalWorkflowAsync(input).ConfigureAwait(false);
        }

        public override async Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
        {
            await root.EncryptAsync(input.Args).ConfigureAwait(false);
            return await base.QueryWorkflowAsync<TResult>(input).ConfigureAwait(false);
        }
    }

    private sealed class ActivityInbound : ActivityInboundInterceptor
    {
        private readonly SecretEncryptionInterceptor root;

        public ActivityInbound(SecretEncryptionInterceptor root, ActivityInboundInterceptor next)
            : base(next) => this.root = root;

        public override async Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            await root.DecryptAsync(input.Args).ConfigureAwait(false);
            return await base.ExecuteActivityAsync(input).ConfigureAwait(false);
        }
    }
}
