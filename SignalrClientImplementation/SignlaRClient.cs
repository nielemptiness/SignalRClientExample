using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Log = Serilog.Log;

namespace SignalrClientImplementation
{
    public class SignalRClient
    {
        private HubConnection _connection;
        public static string ApiKey { get; set; }
        private bool SnapshotRecieved { get; set; }
        private List<Message> _messages = new();
        
        public async Task SubscribeChannelAsync(string hub = "Your link")
        {
            if (_connection != null && (_connection.State != HubConnectionState.Connected))
            {
                await _connection.DisposeAsync();
            }

            Log.Information($"Creating connection to {hub}");
            
            _connection = new HubConnectionBuilder()
                .WithUrl(hub, options =>
                {
                    //Or your custom auth
                    options.Headers["ApiKey"] = ApiKey;
                    
                    options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(5) })
                .Build();

            _connection.On<JsonElement[]>("OnUpdate", MessagesHandler);
            _connection.Closed += _connection_Closed;
            _connection.Reconnecting += Reconnecting;
            
            try
            {
                await _connection.StartAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }

            Log.Information("Successfully connected!");
        }

        public async Task DisconnectAsync()
        {
            if (_connection != null && (_connection.State == HubConnectionState.Disconnected))
            {
                await _connection.DisposeAsync();
                return;
            }
            try
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                throw;
            }
        }

        private Task _connection_Closed(Exception ex)
        {
            if (ex != null)
            {
                Log.Error(ex.Message);
                throw ex;
            }

            Log.Warning("Disconnected");
            return Task.CompletedTask;
        }

        private void MessagesHandler(JsonElement[] messages)
        {
            foreach (var message in messages)
            {
                var update = JsonSerializer.Serialize(message, new JsonSerializerOptions 
                    { 
                        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic), 
                        WriteIndented = true 
                    });
                _createMessage(update);
            }

            if(_messages.Count > 0 && !SnapshotRecieved) 
                SnapshotRecieved = true;
        }

        private void _createMessage(string text)
        {
            _messages.Add(new Message() { Date = DateTime.Now, Text = text });
        }


        private Task Reconnecting(Exception ex)
        {
            if (_connection.State == HubConnectionState.Reconnecting)
            {
                Log.Warning("Trying to reconnect!");
                if(ex != null) Log.Error(ex.Message);
            }

            return Task.CompletedTask;
        }

        public HubConnection GetConnection()
        {
            return _connection;
        }

        public bool GetSnapStatus()
        {
            return SnapshotRecieved;
        }

        public List<Message> GetMessages()
        {
            return _messages;
        }
    }
    public class Message
    {
        public DateTime Date { get; set; }
        public string Text { get; set; }
    }
}