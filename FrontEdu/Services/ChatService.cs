using Microsoft.AspNetCore.SignalR.Client;
using FrontEdu.Models.Chat;

namespace FrontEdu.Services
{
    public class ChatService
    {
        private readonly HubConnection _hubConnection;
        public event Action<MessageDto> OnMessageReceived;
        public event Action<MessageDto> OnMessageUpdated;
        public event Action<int> OnMessageDeleted;

        public ChatService()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{AppConfig.ApiBaseUrl}chatHub")
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<MessageDto>("ReceiveMessage", (message) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnMessageReceived?.Invoke(message);
                });
            });

            _hubConnection.On<MessageDto>("MessageUpdated", (message) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnMessageUpdated?.Invoke(message);
                });
            });

            _hubConnection.On<int>("MessageDeleted", (messageId) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnMessageDeleted?.Invoke(messageId);
                });
            });
        }

        public async Task StartAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                _hubConnection.Headers.Add("Authorization", $"Bearer {token}");
                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SignalR connection error: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            try
            {
                await _hubConnection.StopAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SignalR disconnection error: {ex.Message}");
            }
        }
    }
}