using System.Net.Http.Json;
using Data;
using Org.BouncyCastle.Cms;

namespace Client;

/// <summary>
/// A client for the simple web server
/// </summary>
public class ChatClient
{
    private readonly HttpClient httpClient;
    private readonly string alias;
    readonly CancellationTokenSource cancellationTokenSource = new();

    public ChatClient(string alias, Uri serverUri)
    {
        this.alias = alias;
        this.httpClient = new HttpClient();
        this.httpClient.BaseAddress = serverUri;
    }

    public async Task<bool> Connect()
    {
        var message = new ChatMessage { Sender = this.alias, Content = $"Hi, I joined the chat!" };
        var response = await this.httpClient.PostAsJsonAsync("/messages", message);

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Sends a new message into the chat.
    /// </summary>
    /// <param name="content">The message content as text.</param>
    /// <returns>True if the message could be sent; otherwise False</returns>
    public async Task<bool> SendMessage(string content,string? recipient = null)
    {
		if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
		{
			Console.WriteLine("Die Nachricht ist ungültig oder zu lang.");
			return false;
		}

		var message = new ChatMessage
		{
			Sender = this.alias,
			Content = content.Trim(),
			Recipient = recipient,
			Timestamp = DateTime.Now
		};

		var response = await this.httpClient.PostAsJsonAsync("/messages", message);
		return response.IsSuccessStatusCode;
	}

    /// <summary>
    /// Sends a private message to a specific recipient.
    /// </summary>
    /// <param name="recipient">The recipient of the private message.</param>
    /// <param name="content">The content of the private message.</param>
    /// <returns>True if the message could be sent; otherwise False</returns>
    public async Task<bool> SendPrivateMessage(string recipient, string content)
    {
        // Creates a chat message with sender, content, and recipient (for private messages).
        var message = new ChatMessage { Sender = this.alias, Content = content, Recipient = recipient };
        // Sends the message to the server using a POST request.
        var response = await this.httpClient.PostAsJsonAsync("/messages", message);
        // Returns whether the message was sent successfully.
        return response.IsSuccessStatusCode;
    }
	    
    /// <summary>
    /// Sends a weather command to the server.
    /// </summary>
    /// <param name="content">The command content as text.</param>
    /// <returns>Response with string message and success status.</returns>
    public async Task<Response> SendWeatherMessage(string content)
    {
        // Creates the message and sends it to the server
        var message = new ChatMessage { Sender = this.alias, Content = content };
        var response = await this.httpClient.PostAsJsonAsync("/messages", message);

        string responseBody = await response.Content.ReadAsStringAsync();
        Response userResponse = new Response { Message = responseBody, Success = response.IsSuccessStatusCode };
        return userResponse;
    }

    /// <summary>
    /// Listens for new messages from the server.
    /// </summary>
    public async Task ListenForMessages()
    {
		var cancellationToken = this.cancellationTokenSource.Token;

		while (true)
		{
			try
			{
				// Nachrichten für diesen Client anhand des Alias abrufen
				var response = await this.httpClient.GetAsync($"/messages?id={this.alias}", cancellationToken);
				if (response.IsSuccessStatusCode)
				{
					// Antwort in ein ChatMessage-Objekt deserialisieren
					var message = await response.Content.ReadFromJsonAsync<ChatMessage>();
					if (message != null && message.Sender != this.alias)
					{
						// Ereignis auslösen und die empfangene Nachricht anzeigen
						this.OnMessageReceived(message.Sender, message.Content, message.Color, message.Timestamp);
					}
				}
			}
			catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Beenden der Verbindung durch Benachrichtigung des Clients
				this.OnMessageReceived("Ich", "Verlasse den Chat", "Grau", DateTime.Now);
				break;
			}
			catch (Exception ex)
			{
				// Fehler beim Abrufen von Nachrichten protokollieren
				Console.WriteLine($"[FEHLER] Nachrichtenabruf fehlgeschlagen: {ex.Message}");
			}
		}
	}

    /// <summary>
    /// Cancels listening for messages, effectively disconnecting the client.
    /// </summary>
    public void CancelListeningForMessages()
    {
        this.cancellationTokenSource.Cancel();
    }

	/// <summary>
	/// Ereignis, das ausgelöst wird, wenn eine Nachricht empfangen wird.
	/// </summary>
	public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

	/// <summary>
	/// Löst das MessageReceived-Ereignis aus und zeigt die empfangene Nachricht in der Konsole an.
	/// </summary>
	/// <param name="sender">Der Absender der Nachricht.</param>
	/// <param name="message">Der Inhalt der Nachricht.</param>
	/// <param name="color">Die Anzeigefarbe der Nachricht.</param>
	/// <param name="timestamp">Der Zeitstempel der Nachricht.</param>
	protected virtual void OnMessageReceived(string sender, string message, string color, DateTime timestamp)
	{
		// Konsolentextfarbe basierend auf der angegebenen Farbe ändern
		Console.ForegroundColor = ParseColor(color);
		Console.WriteLine($"\n[{timestamp:T}] {sender}: {message}");
		Console.ResetColor();

		// Das MessageReceived-Ereignis mit den angegebenen Daten auslösen
		this.MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Sender = sender, Message = message });
	}

	/// <summary>
	/// Konvertiert eine Farbzeichenfolge in eine ConsoleColor.
	/// </summary>
	/// <param name="color">Die Farbzeichenfolge.</param>
	/// <returns>Die entsprechende ConsoleColor. Standardwert ist Weiß, wenn die Konvertierung fehlschlägt.</returns>
	private ConsoleColor ParseColor(string color)
	{
		return Enum.TryParse(color, true, out ConsoleColor consoleColor) ? consoleColor : ConsoleColor.White;
	}
}
