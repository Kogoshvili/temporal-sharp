using Google.Protobuf;
using Kogoshvili.Temporal.Codec;
using Temporalio.Api.Common.V1;

namespace Kogoshvili.Temporal.Codec.Tests;

public class EncryptionCodecTests
{
    private const string Key = "test-key-test-key-test-key-test!";

    [Fact]
    public async Task EncodeThenDecode_RoundTripsPayload()
    {
        var codec = new EncryptionCodec(Key);
        var original = Payload(ByteString.CopyFromUtf8("secret"));

        var encoded = await codec.EncodeAsync(new[] { original });
        var decoded = await codec.DecodeAsync(encoded);

        Assert.Single(decoded);
        Assert.Equal("secret", decoded.Single().Data.ToStringUtf8());
    }

    [Fact]
    public async Task Encode_StampsEncryptionEncodingAndKeyId()
    {
        var codec = new EncryptionCodec(Key, keyId: "my-key");

        var encoded = (await codec.EncodeAsync(new[] { Payload() })).Single();

        Assert.Equal("binary/encrypted", encoded.Metadata["encoding"].ToStringUtf8());
        Assert.Equal("my-key", encoded.Metadata["encryption-key-id"].ToStringUtf8());
    }

    [Fact]
    public async Task Decode_IgnoresForeignEncodings()
    {
        var codec = new EncryptionCodec(Key);
        var foreign = new Payload
        {
            Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") },
            Data = ByteString.CopyFromUtf8("untouched"),
        };

        var decoded = (await codec.DecodeAsync(new[] { foreign })).Single();

        Assert.Equal("untouched", decoded.Data.ToStringUtf8());
        Assert.Equal("json/plain", decoded.Metadata["encoding"].ToStringUtf8());
    }

    [Fact]
    public async Task Decode_WrongKeyId_Throws()
    {
        var codec = new EncryptionCodec(Key, keyId: "expected");
        var encoded = (await codec.EncodeAsync(new[] { Payload() })).Single();
        encoded.Metadata["encryption-key-id"] = ByteString.CopyFromUtf8("other");

        await Assert.ThrowsAsync<InvalidOperationException>(() => codec.DecodeAsync(new[] { encoded }));
    }

    private static Payload Payload(ByteString? data = null) =>
        new() { Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") }, Data = data ?? ByteString.Empty };
}

public class ClaimCheckCodecTests
{
    [Fact]
    public async Task Encode_OffloadsPayloadsOverThreshold()
    {
        var store = new FileSystemClaimCheckStore(NewDirectory());
        var codec = new ClaimCheckCodec(store, thresholdBytes: 50);

        var small = Payload(new string('a', 5));
        var large = Payload(new string('b', 100));

        var encoded = (await codec.EncodeAsync(new[] { small, large })).ToArray();

        Assert.Equal(2, encoded.Length);
        Assert.Equal("aaaaa", encoded[0].Data.ToStringUtf8());
        Assert.Equal("binary/claim-check-ref", encoded[1].Metadata["encoding"].ToStringUtf8());
        Assert.NotEmpty(encoded[1].Data.ToStringUtf8());
    }

    [Fact]
    public async Task EncodeThenDecode_RoundTripsOffloadedPayload()
    {
        var store = new FileSystemClaimCheckStore(NewDirectory());
        var codec = new ClaimCheckCodec(store, thresholdBytes: 10);

        var original = Payload(new string('b', 100));

        var encoded = (await codec.EncodeAsync(new[] { original })).Single();
        var decoded = (await codec.DecodeAsync(new[] { encoded })).Single();

        Assert.Equal(new string('b', 100), decoded.Data.ToStringUtf8());
    }

    private static string NewDirectory() =>
        Path.Combine(Path.GetTempPath(), $"claim-check-{Guid.NewGuid():N}");

    private static Payload Payload(string data) =>
        new() { Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") }, Data = ByteString.CopyFromUtf8(data) };
}

public class CompositePayloadCodecTests
{
    [Fact]
    public async Task Encode_AppliesCodecsInOrder_DecodeReverses()
    {
        var store = new FileSystemClaimCheckStore(Path.Combine(Path.GetTempPath(), $"claim-check-{Guid.NewGuid():N}"));
        var composite = new CompositePayloadCodec(
            new EncryptionCodec("test-key-test-key-test-key-test!"),
            new ClaimCheckCodec(store, thresholdBytes: 1));

        var original = new Payload
        {
            Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") },
            Data = ByteString.CopyFromUtf8("a payload large enough to offload"),
        };

        var encoded = (await composite.EncodeAsync(new[] { original })).Single();

        // The claim-check codec runs last, so the stored blob is ciphertext.
        Assert.Equal("binary/claim-check-ref", encoded.Metadata["encoding"].ToStringUtf8());

        var decoded = (await composite.DecodeAsync(new[] { encoded })).Single();
        Assert.Equal("a payload large enough to offload", decoded.Data.ToStringUtf8());
    }
}

public class FileSystemClaimCheckStoreTests
{
    [Fact]
    public async Task StoreThenLoad_RoundTrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"claim-check-{Guid.NewGuid():N}");
        var store = new FileSystemClaimCheckStore(directory);

        var data = new byte[] { 1, 2, 3, 4 };
        var key = await store.StoreAsync(data);

        Assert.Equal(data, await store.LoadAsync(key));
    }

    [Fact]
    public async Task Load_PathTraversal_Throws()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"claim-check-{Guid.NewGuid():N}");
        var store = new FileSystemClaimCheckStore(directory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync("../outside"));
    }
}
