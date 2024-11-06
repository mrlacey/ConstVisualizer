// <copyright file="ExceptionHelper.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;

namespace ConstVisualizer
{
	public static class ExceptionHelper
	{
		public static void Log(Exception exc, params string[] extraInfo)
		{
			System.Diagnostics.Debug.WriteLine(exc);

			foreach (var item in extraInfo)
			{
				System.Diagnostics.Debug.WriteLine(item);
			}

#if DEBUG
			System.Diagnostics.Debugger.Break();
#endif

			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

			OutputPane.Instance?.WriteLine(string.Empty);
			OutputPane.Instance?.WriteLine("Exception 😢");
			OutputPane.Instance?.WriteLine("-----------");
			OutputPane.Instance?.WriteLine(exc.Message);
			OutputPane.Instance?.WriteLine(exc.Source);
			OutputPane.Instance?.WriteLine(exc.StackTrace);

			foreach (var item in extraInfo)
			{
				OutputPane.Instance?.WriteLine(item);
			}

			OutputPane.Instance?.WriteLine(string.Empty);

			foreach (var item in extraInfo)
			{
				OutputPane.Instance?.WriteLine(item);
			}

			OutputPane.Instance?.WriteLine(string.Empty);
		}
	}
}
