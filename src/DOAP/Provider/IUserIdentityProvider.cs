using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DOAP.Provider
{
	public interface IUserIdentityProvider<out TResourceOwnerIdentity>
	{
		TResourceOwnerIdentity GetUserIdentifier(string username);
	}
}
