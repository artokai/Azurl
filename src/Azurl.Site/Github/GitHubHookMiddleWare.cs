using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Azurl.Extensions;
using Azurl.Core;

namespace Azurl.GitHub
{
    public class GitHubHookMiddleware
    {
        private readonly RequestDelegate next;
        private readonly GitHubConfiguration settings;
        private readonly AliasRegistry aliasRegistry;
        private readonly ILogger<GitHubHookMiddleware> logger;

        public GitHubHookMiddleware(RequestDelegate next, IOptions<GitHubConfiguration> configuration, AliasRegistry aliasRegistry, ILogger<GitHubHookMiddleware> logger)
        {
            this.next = next;
            this.settings = configuration.Value;
            this.aliasRegistry = aliasRegistry;
            this.logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {

            var requestPath = (context.Request.Path.Value ?? "");
            if (requestPath.Equals("/webhooks/github"))
            {
                logger.LogInformation("Received notification from github.");
                var responseCode = await Process(context);
                context.Response.StatusCode = responseCode;
                return;
            }
            await next.Invoke(context);
        }

        private async Task<int> Process(HttpContext context)
        {
            var headers = context.Request.Headers;
            var bodyBytes = await context.Request.Body.ReadAllBytes();

            logger.LogDebug("Verifying request method...");
            if (!context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation($"Github notification uses an invalid method '{context.Request.Method}'!");
                return StatusCodes.Status405MethodNotAllowed;
            }

            logger.LogDebug("Verifying signature...");
            if (!VerifySignature(headers, bodyBytes))
            {
                logger.LogInformation("Github notification has an invalid signature!");
                return StatusCodes.Status400BadRequest;
            }

            logger.LogDebug("Verifying event type...");
            if (!IsPushEvent(headers))
            {
                logger.LogInformation($"Github notification is not a push event!");
                return StatusCodes.Status200OK;
            }

            logger.LogDebug("Parsing event body...");
            dynamic msg = JObject.Parse(Encoding.UTF8.GetString(bodyBytes));

            logger.LogDebug("Verifying repository and branch...");
            if (!VerifyRepositoryAndBranch(msg))
            {
                logger.LogInformation($"Github notification is for different branch or repository!");
                return StatusCodes.Status200OK;
            }

            // Start loading new aliases in the background
            logger.LogInformation("Fetching updated aliases...");
            UpdateAliasesInTheBackground();
            return StatusCodes.Status200OK;
        }

        private void UpdateAliasesInTheBackground()
        {
            Task.Run(() => { aliasRegistry.LoadAliasesFromProvider(); });
        }

        private bool VerifySignature(IHeaderDictionary headers, byte[] bodyBytes)
        {
            // Do not validate signature if secret is not defined
            var secret = settings.HookSecret;
            if (string.IsNullOrEmpty(secret)) {
                logger.LogWarning("GitHub hook secret not defined! Skipping signature verification.");
                return true;
            }

            // Empty signature is not ok
            var signature = headers["X-Hub-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
                return false;
            var parts = signature.Split(new char[] { '=' });
            if (parts.Length != 2 || !string.Equals(parts[0], "sha1", StringComparison.OrdinalIgnoreCase))
                return false;
            var actual = GetBytesFromHexString(parts[1]);

            // Calculate signature from body
            var hMACSHA = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
            var expected = hMACSHA.ComputeHash(bodyBytes);

            // Verify that signature bytes are equal
            return AreBytesEqual(actual, expected);
        }


        private bool IsPushEvent(IHeaderDictionary headers)
        {
            return headers.Any(h => h.Key.Equals("X-GitHub-Event") && h.Value.Any(v => v.Equals("push", StringComparison.OrdinalIgnoreCase)));
        }


        private bool VerifyRepositoryAndBranch(dynamic msg)
        {
            // Verify that the push is to the correct repository
            string actualRepository = msg.repository.full_name;
            string expectedRepository = settings.Repository;
            if (!expectedRepository.Equals(actualRepository, StringComparison.OrdinalIgnoreCase))
                return false;

            // Verify that the push is to the correct branch
            string actualRef = msg.@ref;
            string expectedRef = $"refs/heads/{settings.Branch}";
            if (!expectedRef.Equals(actualRef, StringComparison.OrdinalIgnoreCase))
                return false;

            // All Ok
            return true;
        }


        private byte[] GetBytesFromHexString(string hex)
        {
            var array = new byte[hex.Length / 2];
            int num = 0;
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = Convert.ToByte(new string(new char[] { hex[num++], hex[num++] }), 16);
            }
            return array;
        }

        private bool AreBytesEqual(byte[] inputA, byte[] inputB)
        {
            if (inputA == inputB)
                return true;

            if (inputA == null || inputB == null || inputA.Length != inputB.Length)
                return false;

            bool flag = true;
            for (int i = 0; i < inputA.Length; i++)
                flag &= (inputA[i] == inputB[i]);
            return flag;
        }
    }
}


