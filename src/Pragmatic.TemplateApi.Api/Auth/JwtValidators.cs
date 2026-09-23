using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Pragmatic.TemplateApi.Api.Auth;

public static class TokenValidators
{
    public static bool ValidateAudienceOrClientId(IEnumerable<string> audiences, SecurityToken securityToken, TokenValidationParameters validationParameters)
    {
        // If we are validating a JWT token, add the client_id to the audiences so it is included in the validation pass
        if (securityToken is JwtSecurityToken jwtToken)
        {
            var clientId = jwtToken.Claims.FirstOrDefault(c => c.Type == "client_id");

            if (clientId != null)
                audiences = audiences.Union([clientId.Value]);
        }

        IEnumerable<string> validationParametersAudiences;

        if (validationParameters.ValidAudiences == null)
            validationParametersAudiences = new[] { validationParameters.ValidAudience };
        else if (string.IsNullOrWhiteSpace(validationParameters.ValidAudience))
            validationParametersAudiences = validationParameters.ValidAudiences;
        else
            validationParametersAudiences = validationParameters.ValidAudiences.Concat(new[] { validationParameters.ValidAudience });

        return AudienceIsValid(audiences, validationParameters, validationParametersAudiences);
    }

    private static bool AudienceIsValid(IEnumerable<string> audiences, TokenValidationParameters validationParameters, IEnumerable<string> validationParametersAudiences)
    {
        foreach (string tokenAudience in audiences)
        {
            if (string.IsNullOrWhiteSpace(tokenAudience))
                continue;

            foreach (string validAudience in validationParametersAudiences)
            {
                if (string.IsNullOrWhiteSpace(validAudience))
                    continue;

                if (AudiencesMatch(validationParameters, tokenAudience, validAudience))
                {
                    if (LogHelper.IsEnabled(EventLogLevel.Informational))
                        LogHelper.LogInformation("Audience Validated.Audience: '{0}'", LogHelper.MarkAsNonPII(tokenAudience));

                    return true;
                }
            }
        }

        return false;
    }

    private static bool AudiencesMatch(TokenValidationParameters validationParameters, string tokenAudience, string validAudience)
    {
        if (validAudience.Length == tokenAudience.Length && string.Equals(validAudience, tokenAudience))
        {
            return true;
        }
        else if (validationParameters.IgnoreTrailingSlashWhenValidatingAudience && AudiencesMatchIgnoringTrailingSlash(tokenAudience, validAudience))
        {
            return true;
        }

        return false;
    }

    private static bool AudiencesMatchIgnoringTrailingSlash(string tokenAudience, string validAudience)
    {
        int length = -1;

        if (validAudience.Length == tokenAudience.Length + 1 && validAudience.EndsWith('/'))
            length = validAudience.Length - 1;
        else if (tokenAudience.Length == validAudience.Length + 1 && tokenAudience.EndsWith('/'))
            length = tokenAudience.Length - 1;

        // the length of the audiences is different by more than 1 and neither ends in a "/"
        if (length == -1)
            return false;

        if (string.CompareOrdinal(validAudience, 0, tokenAudience, 0, length) == 0)
        {
            if (LogHelper.IsEnabled(EventLogLevel.Informational))
                LogHelper.LogInformation("Audience Validated.Audience: '{0}'", LogHelper.MarkAsNonPII(tokenAudience));

            return true;
        }

        return false;
    }
}
