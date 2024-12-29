namespace Data
{
    /// <summary>
    /// A single chat message for various purposes
    /// </summary>
    public class ChatMessage
    {
        public required string Sender { get; set; } // The sender of the message.
        public required string Content { get; set; } // The content of the message.
		public string Color { get; set; }
		public DateTime Timestamp { get; set; } 
		public string? Recipient { get; set; } // Optional recipient for private messages.
    }
}