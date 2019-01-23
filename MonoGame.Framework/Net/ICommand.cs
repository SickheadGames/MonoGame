using System;

namespace Microsoft.Xna.Framework.Net
{
	internal interface ICommand : IDisposable
	{		
		CommandEventType Command { get; }
	}
}

