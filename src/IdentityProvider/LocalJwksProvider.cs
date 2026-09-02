using Microsoft.IdentityModel.Tokens;

namespace IdentityProvider;

/// <summary>
/// Предоставляет ключи подписи локально, без HTTP-запроса к самому себе (избегает deadlock).
/// </summary>
public sealed class LocalJwksProvider : SharedAuth.IJwksProvider
{
    private readonly TokenService _tokenService;

    public LocalJwksProvider(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public IEnumerable<SecurityKey> GetSigningKeys()
        => _tokenService.GetJwks().GetSigningKeys();
}
