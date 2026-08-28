using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Resolves a <see cref="JsonConverter{T}"/> for the open generic
/// <see cref="Secret{T}"/> type, wiring the JSON representation of an encrypted
/// secret to the <c>encoding</c>/<c>encryption-key-id</c>/<c>data</c> shape.
/// </summary>
public sealed class SecretJsonConverterFactory : JsonConverterFactory
{
    private const string Encoding = "binary/encrypted";

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Secret<>);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(SecretJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class SecretJsonConverter<T> : JsonConverter<Secret<T>>
    {
        public override Secret<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? keyId = null;
            byte[]? data = null;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected a secret object.");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var property = reader.GetString();
                reader.Read();
                switch (property)
                {
                    case "encryption-key-id":
                        keyId = reader.GetString();
                        break;
                    case "data":
                        data = reader.GetBytesFromBase64();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (data is null || string.IsNullOrEmpty(keyId))
            {
                throw new JsonException("The secret object is missing its ciphertext or key id.");
            }

            return Secret<T>.FromCiphertext(data, keyId);
        }

        public override void Write(Utf8JsonWriter writer, Secret<T> value, JsonSerializerOptions options)
        {
            if (!value.IsEncrypted)
            {
                throw new InvalidOperationException(
                    "A plaintext secret cannot be serialized. Encrypt it first via SecretEncryptionInterceptor.");
            }

            writer.WriteStartObject();
            writer.WriteString("encoding", Encoding);
            writer.WriteString("encryption-key-id", value.KeyId);
            writer.WriteBase64String("data", value.Ciphertext);
            writer.WriteEndObject();
        }
    }
}
