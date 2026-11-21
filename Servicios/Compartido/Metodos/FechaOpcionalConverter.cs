using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Compartido.Metodos
{
    /// <summary>
    /// Permite que un campo DateTime llegue en null, vacío o sin enviarse:
    /// en esos casos queda en DateTime.MinValue en vez de responder 400.
    /// Si la fecha llega con valor se lee normal.
    /// </summary>
    public class FechaOpcionalConverter : JsonConverter<DateTime>
    {
        // Necesario para que System.Text.Json pase el null al convertidor en un tipo no nullable
        public override bool HandleNull => true;

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType == JsonTokenType.String)
            {
                var texto = reader.GetString();

                if (string.IsNullOrWhiteSpace(texto))
                    return default;

                if (reader.TryGetDateTime(out var fecha))
                    return fecha;

                if (DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                    return fecha;
            }

            throw new JsonException($"El valor no es una fecha válida para {typeToConvert.Name}.");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
