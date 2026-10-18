using System.Buffers.Text;
using System.Text.Json;
using SearchLab.Core.Results;

namespace SearchLab.Core.Search;

public sealed class CursorCodec<TState>(string backendName)
{
    private readonly string _backendName = backendName;

    public string Encode(TState state)
    {
        var envelope = new Envelope(_backendName, JsonSerializer.SerializeToElement(state));

        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(envelope));
    }

    public Result<TState> Decode(string token)
    {
        // State stays a JsonElement until the backend stamp is checked, so a foreign cursor
        // reports the wrong backend instead of failing on its unfamiliar shape.
        Envelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(Base64Url.DecodeFromChars(token));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return Error.Validation("search.cursor_invalid", $"Cursor could not be decoded: {ex.Message}");
        }

        if (envelope is null)
        {
            return Error.Validation("search.cursor_invalid", "Cursor payload was empty.");
        }

        if (envelope.Backend != _backendName)
        {
            return Error.Validation(
                "search.cursor_wrong_backend",
                $"Cursor was issued by '{envelope.Backend}', not '{_backendName}'.");
        }

        try
        {
            TState? state = envelope.State.Deserialize<TState>();

            if (state is null)
            {
                return Error.Validation("search.cursor_invalid", "Cursor state could not be deserialized.");
            }

            return state;
        }
        catch (JsonException ex)
        {
            return Error.Validation("search.cursor_invalid", $"Cursor state could not be deserialized: {ex.Message}");
        }
    }

    private sealed record Envelope(string Backend, JsonElement State);
}
