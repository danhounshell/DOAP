﻿//-----------------------------------------------------------------------
// <copyright file="OAuthProvider.cs" company="">
//    Copyright (c) Tony Williams 2010. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace DOAP
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using Context;
  using Provider;
  using Response;

  /// <summary>
  /// Defines the methods required to implement an OAuth 2.0 - Draft #10 Resource/Authorization Server
  /// http://tools.ietf.org/html/draft-ietf-oauth-v2-10
  /// </summary>
  public class OAuthProvider<TClientIdentity, TResourceOwnerIdentity>
  {
    // I'm sure I can make this class cleaner. Will have to spend some time doing it.

    private readonly IClientProvider<TClientIdentity> clientProvider;
    private readonly ITokenProvider<TClientIdentity, TResourceOwnerIdentity> tokenProvider;
    private readonly IAuthorizationProvider<TClientIdentity, TResourceOwnerIdentity> authorizationProvider;
    private readonly IPasswordProvider<TResourceOwnerIdentity> passwordProvider;
	private readonly IUserIdentityProvider<TResourceOwnerIdentity> userIdentityProvider;
    private readonly IAssertionProvider<TResourceOwnerIdentity> assertionProvider;
    private readonly IEnumerable<GrantType> supportedGrantTypes;
    private readonly IEnumerable<ResponseType> supportedResponseTypes;
    private readonly IEnumerable<string> supportedScopes;
    private readonly TimeSpan? accessTokenExpirationTime;
    private readonly TimeSpan authorizationTokenExpirationTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthProvider&lt;TClientIdentity, TResourceOwnerIdentity&gt;"/> class.
    /// </summary>
    /// <param name="clientProvider">The client provider.</param>
    /// <param name="tokenProvider">The token provider.</param>
    /// <param name="authorizationProvider">The authorization provider.</param>
    /// <param name="passwordProvider">The password provider.</param>
    /// <param name="assertionProvider">The assertion provider.</param>
    /// <param name="supportedGrantTypes">The supported grant types.</param>
    /// <param name="supportedScopes">The supported scopes.</param>
    /// <param name="supportedResponseTypes">The supported response types.</param>
    /// <param name="accessTokenExpirationTime">The access token expiration time.</param>
    /// <param name="authorizationTokenExpirationTime">The authorization token expiration time.</param>
    public OAuthProvider(IClientProvider<TClientIdentity> clientProvider,
      ITokenProvider<TClientIdentity, TResourceOwnerIdentity> tokenProvider,
      IAuthorizationProvider<TClientIdentity, TResourceOwnerIdentity> authorizationProvider,
      IPasswordProvider<TResourceOwnerIdentity> passwordProvider,
	  IUserIdentityProvider<TResourceOwnerIdentity> userIdentityProvider,
      IAssertionProvider<TResourceOwnerIdentity> assertionProvider,
      IEnumerable<GrantType> supportedGrantTypes, 
      IEnumerable<string> supportedScopes, 
      IEnumerable<ResponseType> supportedResponseTypes,
      TimeSpan? accessTokenExpirationTime,
      TimeSpan authorizationTokenExpirationTime)
    {
      this.clientProvider = clientProvider;
      this.tokenProvider = tokenProvider;
      this.authorizationProvider = authorizationProvider;
      this.passwordProvider = passwordProvider;
	  this.userIdentityProvider = userIdentityProvider;
      this.assertionProvider = assertionProvider;
      this.supportedGrantTypes = supportedGrantTypes ?? new List<GrantType>();
      this.supportedScopes = supportedScopes ?? new List<string>();
      this.supportedResponseTypes = supportedResponseTypes ?? new List<ResponseType>();
      this.accessTokenExpirationTime = accessTokenExpirationTime;
      this.authorizationTokenExpirationTime = authorizationTokenExpirationTime;
    }

    /// <summary>
    /// Returns an <see cref="AuthorizationCode{TClientIdentity,TResourceOwnerIdentity}"/> that shows what the client is requesting access to and in what scope(s)
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>An <see cref="AuthorizationCode{TClientIdentity,TResourceOwnerIdentity}"/> with the access or error</returns>
    /// <remarks>http://tools.ietf.org/html/draft-ietf-oauth-v2-10#section-3</remarks>
    public AuthorizationResponse<TClientIdentity> RequestedAuthorization(IAuthorizationContext<TClientIdentity> context)
    {
      // Verify the data in the context
      if(context.ResponseType == ResponseType.Unknown)
      {
        return new AuthorizationResponse<TClientIdentity>
        {
          ErrorCode = ErrorCode.InvalidRequest,
          RedirectUri = context.RedirectUri,
          State = context.State
        };
      }

      if(!this.supportedResponseTypes.Contains(context.ResponseType))
      {
        return new AuthorizationResponse<TClientIdentity>
        {
          ErrorCode = ErrorCode.UnSupportedResponseType,
          RedirectUri = context.RedirectUri,
          State = context.State
        };
      }

      var client = this.clientProvider.FindClientById(context.ClientId);
      if (client == null)
      {
        return new AuthorizationResponse<TClientIdentity>
                 {
                   ErrorCode = ErrorCode.InvalidClient,
                   RedirectUri = context.RedirectUri,
                   State = context.State
                 };
      }

      if(context.RedirectUri == null || !client.RedirectUri.Contains(context.RedirectUri))
      {
        return new AuthorizationResponse<TClientIdentity>
        {
          ErrorCode = ErrorCode.RedirectUriMismatch,
          RedirectUri = context.RedirectUri,
          State = context.State
        };
      }

      if(!this.IsSupportedScope(context.Scope))
      {
        return new AuthorizationResponse<TClientIdentity>
        {
          ErrorCode = ErrorCode.UnSupportedGrantType,
          RedirectUri = context.RedirectUri,
          State = context.State
        };
      }

      return new AuthorizationResponse<TClientIdentity>
               {
                 Client = client,
                 Scope = context.Scope,
                 RedirectUri = context.RedirectUri,
                 State = context.State
               };
    }

    /// <summary>
    /// Authorizes the request.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="responseType">Type of the response.</param>
    /// <param name="resourceOwnerId">The resource owner id.</param>
    /// <param name="expiresIn">The expires in.</param>
    /// <returns>The uri to redirect to</returns>
    /// <remarks>http://tools.ietf.org/html/draft-ietf-oauth-v2-10#section-3</remarks>
    public Uri AuthorizeRequest(AuthorizationResponse<TClientIdentity> response, ResponseType responseType, TResourceOwnerIdentity resourceOwnerId, int? expiresIn)
    {
      string token = (responseType == ResponseType.CodeAndToken || responseType == ResponseType.Token)
                       ? this.tokenProvider.GenerateToken()
                       : null;
      string code = (responseType == ResponseType.CodeAndToken || responseType == ResponseType.Code)
                      ? this.tokenProvider.GenerateToken()
                      : null;

      var uri = BuildRedirectUri(response.RedirectUri, code, token, expiresIn, response.Scope, response.State);
      var authorizationCode = new AuthorizationCode<TClientIdentity, TResourceOwnerIdentity>
                                {
                                  Code = code,
                                  RedirectUri = response.RedirectUri,
                                  TimeStamp = DateTime.UtcNow,
                                  ResourceOwnerId = resourceOwnerId,
								  ClientId = response.Client.Id
                                };

      authorizationCode.Expires = authorizationCode.TimeStamp + this.authorizationTokenExpirationTime;

      this.authorizationProvider.StoreAuthorizeClient(authorizationCode);
      return uri;
    }

    /// <summary>
    /// Grants an access token.
    /// </summary>
    /// <param name="tokenContext">The tokenContext.</param>
    /// <returns>Either an actual token or an error</returns>
    /// <remarks>http://tools.ietf.org/html/draft-ietf-oauth-v2-10#section-4</remarks>
    public AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity> GrantAccessToken(ITokenContext<TClientIdentity> tokenContext)
    {
		// Check the request actually has a grant request
		if(tokenContext.GrantType == GrantType.Unknown)
			return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
					{
						ErrorCode = ErrorCode.InvalidRequest
					};

		// Does the server support the grant type in the request?
		if (!supportedGrantTypes.Contains(tokenContext.GrantType))
		{
			if (tokenContext.GrantType == GrantType.ClientCredentials && IsRequestForAnonymousUser(tokenContext))
			{
				// do nothing
			}
			else return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
			{
				ErrorCode = ErrorCode.UnSupportedGrantType
			};
		}

    	// Does the client exist and do the secrets match
		var client = this.clientProvider.FindClientById(tokenContext.ClientId);
		if (client == null)
			return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
			{
				ErrorCode = ErrorCode.InvalidClient
			};

		// Validate the secret anytime its provided in the request
		if (!string.IsNullOrWhiteSpace(tokenContext.ClientSecret) && client.Secret != tokenContext.ClientSecret)
			return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
			{
				ErrorCode = ErrorCode.InvalidClient
			};

		if ((tokenContext.GrantType == GrantType.AuthorizationCode || tokenContext.GrantType == GrantType.ClientCredentials) && client.Secret != tokenContext.ClientSecret)
			return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
			{
				ErrorCode = ErrorCode.InvalidClient
			};

		if (!client.AllowedGrantTypes.Any(g => g == tokenContext.GrantType))
		{
			if (tokenContext.GrantType == GrantType.ClientCredentials && IsRequestForAnonymousUser(tokenContext)) 
			{
				// do nothing
			} 
			else return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
		   	{
		  		ErrorCode = ErrorCode.InvalidGrant
		  	};
		}

    	ErrorCode validRequest;
		AccessToken<TClientIdentity, TResourceOwnerIdentity> refreshToken = null;
		TResourceOwnerIdentity resourceOwnerIdentity = default(TResourceOwnerIdentity);

		// Make sure the tokenContext is valid against the requested (and supported) grant types
		switch (tokenContext.GrantType)
		{
		case GrantType.AuthorizationCode:
			validRequest = this.VerifyAuthorizationCodeParameters(tokenContext, out resourceOwnerIdentity);
			break;
		case GrantType.Password:
			validRequest = this.VerifyPasswordParameters(tokenContext, out resourceOwnerIdentity);
			break;
		case GrantType.RefreshToken:
			validRequest = this.VerifyRefreshTokenParameters(tokenContext, out refreshToken, out resourceOwnerIdentity);
			break;
		case GrantType.ClientCredentials:
			validRequest = this.VerifyClientCredentialParameters(tokenContext, out resourceOwnerIdentity);
			break;
		case GrantType.None:
			validRequest = ErrorCode.None;
			break;
		default:
			validRequest = ErrorCode.InvalidRequest;
			break;
		}

		// Check the request is valid
		if (validRequest != ErrorCode.None)
		{
			// return an error response indicating why the request is invalid
			return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
			{
				ErrorCode = validRequest
			};
		}

		// Check that the server implements the scope required
		if(!this.IsSupportedScope(tokenContext.Scope))
		{
			return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
					{
					ErrorCode = ErrorCode.InvalidScope
					};
		}

		// Everything is good with the request. 		
		// Let's see if an existing access token exists
    	AccessToken<TClientIdentity, TResourceOwnerIdentity> accessToken = tokenProvider.FindAccessToken(client.Id, resourceOwnerIdentity, tokenContext.Scope);

		if (accessToken != null) 
		{
			// if it hasn't expired then do nothing, we'll just return it
			// if the token has expired then generate a new token value, update timestamp, set a new expiration, and save
			if (accessToken.Expires.HasValue && accessToken.Expires.Value <= DateTime.UtcNow) 
			{
				accessToken.Token = this.tokenProvider.GenerateToken();
				accessToken.TimeStamp = DateTime.UtcNow;

				if (this.accessTokenExpirationTime.HasValue)
					accessToken.Expires = accessToken.TimeStamp + this.accessTokenExpirationTime.Value;

				this.tokenProvider.StoreAccessToken(accessToken);
			}
		} 
		else
		{
			// if not then lets give them a token.
			accessToken = new AccessToken<TClientIdentity, TResourceOwnerIdentity>
			              	{
			              		Token = this.tokenProvider.GenerateToken(),
			              		TimeStamp = DateTime.UtcNow,
			              		Scope = tokenContext.Scope,
			              		ClientId = client.Id,
			              		ResourceOwnerId = resourceOwnerIdentity,
								RefreshToken = tokenProvider.GenerateToken()
			              	};

			if (this.accessTokenExpirationTime.HasValue)
				accessToken.Expires = accessToken.TimeStamp + this.accessTokenExpirationTime.Value;			

			// Expire the old access token if we're going to gen a new one
			if (refreshToken != null)
				this.tokenProvider.ExpireAccessToken(refreshToken);			

			// save our token
			this.tokenProvider.StoreAccessToken(accessToken);
		}

    	return new AccessTokenResponse<TClientIdentity, TResourceOwnerIdentity>
				{
					AccessToken = accessToken,
				};
    }

	private bool IsRequestForAnonymousUser(ITokenContext<TClientIdentity> tokenContext)
	{
		if (tokenContext.Username.ToLowerInvariant() == "anonymous")
			return true;
		return false;
	}

    /// <summary>
    /// Verifies the token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The response of the verification process</returns>
    public VerificationResponse<TResourceOwnerIdentity> VerifyToken(string token, string scope)
    {
      var accessToken = this.tokenProvider.FindAccessToken(token);
      if(accessToken == null)
      {
        return new VerificationResponse<TResourceOwnerIdentity> { ErrorCode = ErrorCode.InvalidToken };
      }

      if(accessToken.Expires.HasValue && accessToken.Expires.Value < DateTime.UtcNow)
      {
        return new VerificationResponse<TResourceOwnerIdentity> { ErrorCode = ErrorCode.ExpiredToken };

      }

      if (this.supportedScopes.Any() && !accessToken.Scope.Contains(scope))
      {
        return new VerificationResponse<TResourceOwnerIdentity> { ErrorCode = ErrorCode.InsufficientScope };
      }

       return new VerificationResponse<TResourceOwnerIdentity> { ResourceOwnerId = accessToken.ResourceOwnerId};

    }

    /// <summary>
    /// Determines whether [is supported scope] [the specified token context].
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <returns>
    /// <c>true</c> if [is supported scope] [the specified token context]; otherwise, <c>false</c>.
    /// </returns>
    private bool IsSupportedScope(IEnumerable<string> scope)
    {
      if(!this.supportedScopes.Any() && !scope.Any())
      {
        return true;
      }

      return scope.All(x => !string.IsNullOrWhiteSpace(x) && this.supportedScopes.Contains(x));
    }

    /// <summary>
    /// Builds the redirect URI.
    /// </summary>
    /// <param name="baseUri">The base URI.</param>
    /// <param name="code">The code.</param>
    /// <param name="accessToken">The access token.</param>
    /// <param name="expiresIn">The expires in.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="state">The state.</param>
    /// <returns>
    /// The redirected uri with the query section populated with the oauth data
    /// </returns>
    private static Uri BuildRedirectUri(Uri baseUri, string code, string accessToken, int? expiresIn, IEnumerable<string> scope, string state)
    {
      var query = string.Empty;

      if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(accessToken))
      {
        query += string.Format("code={0}&access_token={1}", Uri.EscapeDataString(code), Uri.EscapeDataString(accessToken));   
      }
      else if (!string.IsNullOrWhiteSpace(code))
      {
        query += string.Format("code={0}", Uri.EscapeDataString(code));
      }
      else
      {
        query += string.Format("access_token={0}", Uri.EscapeDataString(accessToken));
      }

      if(expiresIn.HasValue)
      {
        query += string.Format("&expires_in={0}", expiresIn);
      }

      if (scope != null && !scope.Any(x => string.IsNullOrWhiteSpace(x)))
      {
        var scopeBuilder = new StringBuilder();
        foreach (var s in scope)
        {
          scopeBuilder.AppendFormat("{0},", s);
        }

        // remove last comma
        scopeBuilder.Remove(scopeBuilder.Length - 1, 1);
        query += string.Format("&scope={0}", Uri.EscapeDataString(scopeBuilder.ToString()));
      }

      if (!string.IsNullOrWhiteSpace(state))
      {
        query += string.Format("&state={0}", Uri.EscapeDataString(state));
      }

      var builder = new UriBuilder(baseUri);

      if(builder.Query.Length > 1)
      {
		  builder.Query = builder.Query.Substring(1) + "&" + query;
      }
      else
      {
        builder.Query = query;
      }

      return builder.Uri;
    }

    /// <summary>
    /// Verifies the authorization code parameters.
    /// </summary>
    /// <param name="tokenContext">The tokenContext.</param>
    /// <param name="resourceOwnerIdentity">The resource owner identity.</param>
    /// <returns>The error (if any)</returns>
    private ErrorCode VerifyAuthorizationCodeParameters(ITokenContext<TClientIdentity> tokenContext, out TResourceOwnerIdentity resourceOwnerIdentity)
    {
      resourceOwnerIdentity = default(TResourceOwnerIdentity);

      // Invalid request
      if(string.IsNullOrWhiteSpace(tokenContext.Code) || tokenContext.RedirectUri == null)
      {
        return ErrorCode.InvalidRequest;
      }

      var authCode = this.authorizationProvider.FindAuthorizationCode(tokenContext.Code);

      // Invalid grant
      if(authCode == null || authCode.RedirectUri != tokenContext.RedirectUri || !EqualityComparer<TClientIdentity>.Default.Equals(authCode.ClientId, tokenContext.ClientId))
      {
        return ErrorCode.InvalidGrant;
      }

      // Auth code has expired
      if(authCode.Expires < DateTime.UtcNow)
      {
        return ErrorCode.ExpiredToken;
      }

      resourceOwnerIdentity = authCode.ResourceOwnerId;
      return ErrorCode.None;
    }

    /// <summary>
    /// Verifies the password parameters.
    /// </summary>
    /// <param name="tokenContext">The token context.</param>
    /// <param name="resourceOwnerIdentity">The resource owner identity.</param>
    /// <returns>The error (if any)</returns>
    private ErrorCode VerifyPasswordParameters(ITokenContext<TClientIdentity> tokenContext, out TResourceOwnerIdentity resourceOwnerIdentity)
    {
		resourceOwnerIdentity = default(TResourceOwnerIdentity);

		// Check the username/password parameters exist
		if (string.IsNullOrWhiteSpace(tokenContext.Username) || string.IsNullOrWhiteSpace(tokenContext.Password))
			return ErrorCode.InvalidRequest;

		var user = this.passwordProvider.CheckResourceOwnerCredentials(tokenContext.Username, tokenContext.Password);
		if (EqualityComparer<TResourceOwnerIdentity>.Default.Equals(user, default(TResourceOwnerIdentity)))
			return ErrorCode.InvalidGrant;

		resourceOwnerIdentity = user;
		return ErrorCode.None;
    }

	private ErrorCode VerifyClientCredentialParameters(ITokenContext<TClientIdentity> tokenContext, out TResourceOwnerIdentity resourceOwnerIdentity)
	{
		resourceOwnerIdentity = default(TResourceOwnerIdentity);
		if (string.IsNullOrWhiteSpace(tokenContext.Username))
			return ErrorCode.InvalidRequest;

		var userId = this.userIdentityProvider.GetUserIdentifier(tokenContext.Username);
		if (EqualityComparer<TResourceOwnerIdentity>.Default.Equals(userId, default(TResourceOwnerIdentity)))
			return ErrorCode.InvalidGrant;

		resourceOwnerIdentity = userId;
		return ErrorCode.None;
	}

    /// <summary>
    /// Verifies the assertion parameters.
    /// </summary>
    /// <param name="tokenContext">The token context.</param>
    /// <param name="resourceOwnerIdentity">The resource owner identity.</param>
    /// <returns>The error (if any)</returns>
    private ErrorCode VerifyAssertionParameters(ITokenContext<TClientIdentity> tokenContext, out TResourceOwnerIdentity resourceOwnerIdentity)
    {
      resourceOwnerIdentity = default(TResourceOwnerIdentity);

      if (string.IsNullOrWhiteSpace(tokenContext.Assertion) || string.IsNullOrWhiteSpace(tokenContext.AssertionType))
      {
        return ErrorCode.InvalidRequest;
      }

      // Check the assertion
      resourceOwnerIdentity = this.assertionProvider.ValidateAssertion(tokenContext.Assertion, tokenContext.AssertionType);
      if(EqualityComparer<TResourceOwnerIdentity>.Default.Equals(resourceOwnerIdentity, default(TResourceOwnerIdentity)))
      {
        return ErrorCode.InvalidGrant;
      }

      return ErrorCode.None;
    }

    /// <summary>
    /// Verifies the refresh token parameters.
    /// </summary>
    /// <param name="tokenContext">The token context.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="resourceOwnerIdentity">The resource owner identity.</param>
    /// <returns>The error (if any)</returns>
    private ErrorCode VerifyRefreshTokenParameters(ITokenContext<TClientIdentity> tokenContext, out AccessToken<TClientIdentity, TResourceOwnerIdentity> refreshToken,  out TResourceOwnerIdentity resourceOwnerIdentity)
    {
      resourceOwnerIdentity = default(TResourceOwnerIdentity);
      refreshToken = null;

      // Make sure we have the refresh token
      if (string.IsNullOrWhiteSpace(tokenContext.RefreshToken))
      {
        return ErrorCode.InvalidRequest;
      }

      refreshToken = this.tokenProvider.FindRefreshToken(tokenContext.RefreshToken);
      // check the token is valid
      if (refreshToken == null || !EqualityComparer<TClientIdentity>.Default.Equals(refreshToken.ClientId, tokenContext.ClientId))
      {
        return ErrorCode.InvalidGrant;
      }

      resourceOwnerIdentity = refreshToken.ResourceOwnerId;
      return ErrorCode.None;
    }
  }
}
