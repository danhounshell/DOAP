//-----------------------------------------------------------------------
// <copyright file="IAuthorizationContext.cs" company="">
//    Copyright (c) Tony Williams 2010. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace DOAP.Context
{
  /// <summary>
  /// Defines the properties required when in authorization
  /// </summary>
  /// <typeparam name="TClientIdentity">The type of the client identity.</typeparam>
  public interface IAuthorizationContext<out TClientIdentity> : IContext<TClientIdentity>
  {
    /// <summary>
    /// Gets the state.
    /// </summary>
    /// <value>The state.</value>
    string State { get; }

    /// <summary>
    /// Gets the type of the response.
    /// </summary>
    /// <value>The type of the response.</value>
    ResponseType ResponseType { get; }
  }
}
