using System.Net.Sockets;

namespace EzBot.Services;

public class NetworkService
{
    private readonly HttpClient _httpClient;

    private async Task<bool> IsInternetAvailableAsync()
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

    public NetworkService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HttpResponseMessage> MakeRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        while (!await IsInternetAvailableAsync())
        {
            // Console.WriteLine("Internet is down. Retrying in 5 seconds...");

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        return await _httpClient.SendAsync(request, cancellationToken);
    }
}
