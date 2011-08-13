﻿/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 */

using System;

using Mosa.Platform.x86.Intrinsic;
using Mosa.Kernel;
using Mosa.Kernel.x86;

namespace Mosa.CoolWorld
{
	/// <summary>
	/// 
	/// </summary>
	public static class Boot
	{
		
		/// <summary>
		/// Mains this instance.
		/// </summary>
		public static void Main()
		{
			Mosa.Kernel.x86.Kernel.Setup();

			Screen.GotoTop();
			Screen.Color = Colors.Yellow;

			Screen.Write(@"MOSA OS Version 1.0 '");
			
			while (true)
			{
				
				
			}
		}

	}
}