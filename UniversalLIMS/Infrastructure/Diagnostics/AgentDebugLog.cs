using System.Text.Json;

namespace UniversalLIMS.Infrastructure.Diagnostics;

internal static class AgentDebugLog
{
    private const string LogPath = @"c:\Users\User\Desktop\Zhytomyr\LIMS\UniversalLIMS\debug-40b8bf.log";
    private const string SessionId = "40b8bf";

    public static void Write(string hypothesisId, string location, string message, object? data = null)
    {
        try
        {
            var payload = new
            {
                sessionId = SessionId,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            File.AppendAllText(LogPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // ignore
        }
    }
}
