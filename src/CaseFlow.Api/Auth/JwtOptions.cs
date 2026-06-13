namespace CaseFlow.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "caseflow-api";
    public string Audience { get; init; } = "caseflow-api";

    // HS256 symmetric key, minimum 32 bytes. The Development value in
    // appsettings is intentionally fake; the server gets a generated one via
    // the Jwt__SigningKey environment variable and it never lives in git.
    public string SigningKey { get; init; } = string.Empty;
}
