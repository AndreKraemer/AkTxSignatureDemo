using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace AkTxSignatureDemo.Server.Filters;

/// <summary>
/// Action filter attribute that validates a one-time signature token passed via the
/// <c>signatureToken</c> query parameter. The token must have been issued by the
/// <c>POST /api/documents/sign-token</c> endpoint and is consumed on first use,
/// preventing replay attacks.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValidateSignatureTokenAttribute : ActionFilterAttribute
{
    /// <summary>
    /// Cache key prefix used to store active signature tokens.
    /// </summary>
    public const string CacheKeyPrefix = "sig_token:";

    /// <inheritdoc />
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var token = context.HttpContext.Request.Query["signatureToken"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new BadRequestObjectResult("Missing signatureToken query parameter.");
            return;
        }

        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = $"{CacheKeyPrefix}{token}";

        if (!cache.TryGetValue(cacheKey, out _))
        {
            context.Result = new UnauthorizedObjectResult("Invalid or expired signature token.");
            return;
        }

        // Consume the token — one-time use only
        cache.Remove(cacheKey);

        base.OnActionExecuting(context);
    }
}
