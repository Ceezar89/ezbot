using System.Net.Sockets;

namespace EzBot.Common;

public static class NetworkUtility
{
    private static async Task<bool> IsInternetAvailableAsync()
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("8.8.8.8", 53); // Google Public DNS
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<HttpResponseMessage> MakeRequestAsync(HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        while (!await IsInternetAvailableAsync())
        {
            // Console.WriteLine("Internet is down. Retrying in 5 seconds...");

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }
}