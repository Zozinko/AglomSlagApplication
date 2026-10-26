using System.Globalization;
using AglomSlagServer.Errors;

namespace AglomSlagServer.Auth;

// Claims are read only from the principal authenticated by JwtBearer middleware.
public sealed class CurrentActor(IHttpContextAccessor http)
{
    public int UserId => Read().UserId;
    public bool Admin => Read().Admin;

    private (int UserId, bool Admin) Read()
    {
        var principal = http.HttpContext?.User;
        var subject = principal?.FindFirst("sub")?.Value;
        var admin = principal?.FindFirst("admin")?.Value;
        if (principal?.Identity?.IsAuthenticated != true ||
            !int.TryParse(subject, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) ||
            admin is not ("true" or "false"))
            throw new ApiException("AUTH_NOT_AUTHENTICATED", "Требуется авторизация.");
        return (userId, admin == "true");
    }

    public void RequireAdmin()
    {
        if (!Admin) throw new ApiException(PublicErrorCodes.Forbidden, "Недостаточно прав.");
    }
}
