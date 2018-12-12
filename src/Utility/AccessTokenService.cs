﻿using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using AdvantageTool.Data;
using IdentityModel.Client;
using LtiAdvantage.IdentityModel.Client;
using LtiAdvantage.Lti;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Utility
{
    /// <summary>
    /// Service available via dependency injection to get an access token from the issuer.
    /// </summary>
    public class AccessTokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Create an instance of the AccessTokenService.
        /// </summary>
        /// <param name="context">The application database context to look up the issuer's token endpoint.</param>
        /// <param name="httpClientFactory">The HttpClient factory.</param>
        public AccessTokenService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Get an access token from the issuer.
        /// </summary>
        /// <param name="issuer">The issuer.</param>
        /// <param name="scope">The scope to request.</param>
        /// <returns>The token response.</returns>
        public async Task<TokenResponse> GetAccessTokenAsync(string issuer, string scope)
        {
            if (issuer.IsMissing())
            {
                return new TokenResponse(new ArgumentNullException(nameof(issuer)));
            }

            if (scope.IsMissing())
            {
                return new TokenResponse(new ArgumentNullException(nameof(scope)));
            }

            var platform = await _context.GetPlatformByIssuerAsync(issuer);
            if (platform == null)
            {
                return new TokenResponse(new Exception("Cannot find platform registration."));
            }

            var httpClient = _httpClientFactory.CreateClient();
            var tokenEndPoint = platform.AccessTokenUrl;

            if (tokenEndPoint.IsMissing())
            {
                var disco = await httpClient.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
                {
                    Address = platform.Issuer,
                    Policy =
                    {
                        Authority = platform.Issuer
                    }
                });
                if (disco.IsError)
                {
                    return new TokenResponse(new Exception(disco.Error));
                }

                tokenEndPoint = disco.TokenEndpoint;
            }

            // Use a signed JWT as client credentials.
            var payload = new JwtPayload();
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, platform.ClientId));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, platform.ClientId));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, tokenEndPoint));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(DateTime.UtcNow.AddSeconds(-5)).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(DateTime.UtcNow.AddMinutes(5)).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, LtiResourceLinkRequest.GenerateCryptographicNonce()));

            var handler = new JwtSecurityTokenHandler();
            var credentials = PemHelper.SigningCredentialsFromPemString(platform.PrivateKey);
            var jwt = handler.WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));

            return await httpClient.RequestClientCredentialsTokenWithJwtAsync(
                    new JwtClientCredentialsTokenRequest
                    {
                        Address = tokenEndPoint,
                        ClientId = platform.ClientId,
                        Jwt = jwt,
                        Scope = scope
                    });
        }

    }
}
