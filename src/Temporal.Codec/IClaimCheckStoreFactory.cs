namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Builds a cloud-backed <see cref="IClaimCheckStore"/> from resolved settings.
/// Implementations (Azure Blob, Amazon S3) ship in
/// <c>Kogoshvili.Temporal.Cloud</c>; the filesystem store is built directly and
/// needs no factory.
/// </summary>
/// <remarks>
/// The factory is synchronous because credential values are already resolved
/// (inline, via an <see cref="ISecretResolver"/>, or by a cloud credential
/// chain) before <see cref="Create"/> is called.
/// </remarks>
public interface IClaimCheckStoreFactory
{
    /// <summary>Gets the store name this factory builds (e.g. <c>azureBlob</c>, <c>s3</c>).</summary>
    string Name { get; }

    /// <summary>Builds the claim-check store from the resolved settings.</summary>
    IClaimCheckStore Create(ClaimCheckStoreSettings settings);
}
