using Azure.Core.Serialization;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
#pragma warning disable CS8600
#pragma warning disable CS8603

namespace BMSD.Accessors.UserInfo;

public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonObjectSerializer _systemTextJsonSerializer;

    public CosmosSystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
    {
        _systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
    }

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.CanSeek && stream.Length == 0)
            {

                return default;

            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }


            return (T)_systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
        }
    }

    public override Stream ToStream<T>(T input)
    {
        MemoryStream streamPayload = new MemoryStream();
        _systemTextJsonSerializer.Serialize(streamPayload, input, typeof(T), default);
        streamPayload.Position = 0;
        return streamPayload;
    }
}
#pragma warning restore CS8600
#pragma warning restore CS8603