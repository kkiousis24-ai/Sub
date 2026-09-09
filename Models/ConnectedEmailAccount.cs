namespace Sub.Api.Models;

public class ConnectedEmailAccount
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string EmailAddress { get; set; } = string.Empty;

    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? TokenExpiresAt { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public bool IsActive { get; set; } = true;
}