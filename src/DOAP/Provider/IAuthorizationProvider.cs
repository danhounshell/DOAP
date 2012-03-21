//-----------------------------------------------------------------------
// <copyright file="IAuthorizationProvider.cs" company="">
//    Copyright (c) Tony Williams 2010. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace DOAP.Provider
{
  /// <summary>
  /// Defines the methods for an Authorization provider
  /// </summary>
  /// <typeparam name="TClientIdentity">The type of the client identity.</typeparam>
  /// <typeparam name="TResourceOwnerIdentity">The type of the resource owner identity.</typeparam>
  public interface IAuthorizationProvider<TClientIdentity, TResourceOwnerIdentity>
  {
    /// <summary>
    /// Stores the authorize client.
    /// </summary>
    /// <param name="response">The response.</param>
    void StoreAuthorizeClient(AuthorizationCode<TClientIdentity, TResourceOwnerIdentity> response);

    /// <summary>
    /// Finds the authorization code.
    /// </summary>
    /// <param name="authorizationCode">The authorization code.</param>
    /// <returns>The authorization code</returns>
    AuthorizationCode<TClientIdentity, TResourceOwnerIdentity> FindAuthorizationCode(string authorizationCode);
  }
}
