namespace Sub.Api.Models;

public class EmailCandidate
{
    public string GmailMessageId { get; set; } = "";
    public string From { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Snippet { get; set; } = "";
    public DateTime? Date { get; set; }
}