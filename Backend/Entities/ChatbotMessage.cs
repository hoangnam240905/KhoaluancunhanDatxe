namespace Backend.Entities;

/// <summary>One line of a logged-in customer's chat with the DriveX assistant (history across visits).</summary>
public class ChatbotMessage
{
    public int ChatbotMessageId { get; set; }
    public int CustomerId { get; set; }
    /// <summary>true = the customer's question, false = the assistant's answer.</summary>
    public bool FromCustomer { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>Assistant answers only: the full answer (cars, links, search) as JSON, to show it again.</summary>
    public string? AnswerJson { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
}
