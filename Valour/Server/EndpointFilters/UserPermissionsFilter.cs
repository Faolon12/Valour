using Valour.Server.Services;
using Valour.Shared.Authorization;

namespace Valour.Server.EndpointFilters;

public class UserPermissionsRequiredFilter : IEndpointFilter
{
    private readonly TokenService _tokenService;
    
    public UserPermissionsRequiredFilter(TokenService tokenService)
    {
        _tokenService = tokenService;
    }
    
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var token = await _tokenService.GetCurrentToken();

        if (token is null)
            return ValourResult.InvalidToken();
        
        var userPermAttr = (UserRequiredAttribute)ctx.HttpContext.Items[nameof(UserRequiredAttribute)];

        foreach (var permEnum in userPermAttr.Permissions)
        {
            var permission = UserPermissions.Permissions[(int)permEnum];
            if (!token.HasScope(permission))
                return ValourResult.LacksPermission(permission);
        }

        return await next(ctx);
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class UserRequiredAttribute : Attribute
{
    public readonly UserPermissionsEnum[] Permissions;

    public UserRequiredAttribute(params UserPermissionsEnum[] permissions)
    {
        this.Permissions = permissions;
    }
}
