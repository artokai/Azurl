using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Azurl.Core
{
    public class AzurlRedirectMiddleware
    {
        private readonly RequestDelegate next;
        private readonly AliasRegistry aliases;
        private readonly ILogger<AzurlRedirectMiddleware> logger;

        public AzurlRedirectMiddleware(RequestDelegate next, AliasRegistry aliases, ILogger<AzurlRedirectMiddleware> logger)
        {
            this.next = next;
            this.aliases = aliases;
            this.logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestPath = (context.Request.Path.Value ?? "").TrimStart(new char[] { '/' });
            logger.LogDebug("Resolving redirect for '{0}'.", requestPath);
            var redirectUrl = aliases.Resolve(requestPath);
            if (!string.IsNullOrEmpty(redirectUrl))
            {
                logger.LogInformation("Redirecting from request '{0}' to '{1}'.", requestPath, redirectUrl);
                context.Response.Redirect(redirectUrl);
                return;
            }
            logger.LogInformation("Redirect for request '{0}' not found.", requestPath);
            await next.Invoke(context);
        }
    }
}
