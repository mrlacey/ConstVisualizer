// <copyright file="ExceptionHelper.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ConstVisualizer
{
    public static class ExceptionHelper
    {
        public static void Log(Exception exc)
        {
            System.Diagnostics.Debug.WriteLine(exc);
            System.Diagnostics.Debugger.Break();
            OutputPane.Instance?.WriteLine(string.Empty);
            OutputPane.Instance?.WriteLine("Exception 😢");
            OutputPane.Instance?.WriteLine("-----------");
            OutputPane.Instance?.WriteLine(exc.Message);
            OutputPane.Instance?.WriteLine(exc.Source);
            OutputPane.Instance?.WriteLine(exc.StackTrace);
            OutputPane.Instance?.WriteLine(string.Empty);
        }
    }
}
