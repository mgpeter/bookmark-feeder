namespace BookmarkFeeder.WebApi.Dtos;

/// <summary>
/// Key metadata. Deliberately carries no secret, so this shape is safe to list. Prefix is the
/// display identity ("bmf_7Kd9") - not enough to authenticate with.
/// </summary>
public record ApiKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt);

/// <summary>
/// The create response, and the ONLY shape that ever carries the plaintext key. Never cache it,
/// never log it, never return it from a list endpoint.
/// </summary>
public record CreatedApiKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string Key);

/// <summary>A null or non-positive ExpiresInDays means the key never expires.</summary>
public record CreateApiKeyRequest(string Name, int? ExpiresInDays);
