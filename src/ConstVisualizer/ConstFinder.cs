// <copyright file="ConstFinder.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ConstVisualizer
{
    internal static partial class ConstFinder
    {
        public static bool HasParsedSolution { get; private set; } = false;

        public static List<(string Key, string Qualification, string Value, string Source)> KnownConsts { get; } = new List<(string Key, string Qualification, string Value, string Source)>();

        public static string[] SearchValues
        {
            get
            {
                return KnownConsts.Select(c => c.Key).ToArray();
            }
        }

        public static async Task TryParseSolutionAsync(IComponentModel componentModel = null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var timer = new Stopwatch();
            timer.Start();

            try
            {
                if (componentModel == null)
                {
                    componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
                }

                if (ConstVisualizerPackage.Instance.Options.AdvancedLogging)
                {
                    OutputPane.Instance.WriteLine($"Parse step 1 duration: {timer.Elapsed}");
                }

                var workspace = (Workspace)componentModel.GetService<VisualStudioWorkspace>();

                if (workspace == null)
                {
                    return;
                }

                if (ConstVisualizerPackage.Instance.Options.AdvancedLogging)
                {
                    OutputPane.Instance.WriteLine($"Parse step 2 duration: {timer.Elapsed}");
                }

                var projectGraph = workspace.CurrentSolution?.GetProjectDependencyGraph();

                if (projectGraph == null)
                {
                    return;
                }

                if (ConstVisualizerPackage.Instance.Options.AdvancedLogging)
                {
                    OutputPane.Instance.WriteLine($"Parse step 3 duration: {timer.Elapsed}");
                }

                await Task.Yield();

                var projects = projectGraph.GetTopologicallySortedProjects();

                if (ConstVisualizerPackage.Instance.Options.AdvancedLogging)
                {
                    OutputPane.Instance.WriteLine($"Parse step 4 duration: {timer.Elapsed}");
                }

                foreach (ProjectId projectId in projects)
                {
                    var projectCompilation = await workspace.CurrentSolution?.GetProject(projectId).GetCompilationAsync();

                    if (ConstVisualizerPackage.Instance.Options.AdvancedLogging)
                    {
                        OutputPane.Instance.WriteLine($"Parse loop step duration: {timer.Elapsed} ({projectId})");
                    }

                    if (projectCompilation != null)
                    {
                        foreach (var compiledTree in projectCompilation.SyntaxTrees)
                        {
                            await Task.Yield();

                            GetConstsFromSyntaxRoot(await compiledTree.GetRootAsync(), compiledTree.FilePath);
                        }
                    }
                }

                HasParsedSolution = true;
            }
            catch (Exception exc)
            {
                // Exceptions can happen in the above when a solution is modified before the package has finished loading :(
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ExceptionHelper.Log(exc);

                // Recovery from the above would be very difficult so easiest to prompt to trigger for reparsing later.
                HasParsedSolution = true;
            }
            finally
            {
                timer.Stop();

                await OutputPane.Instance.WriteAsync($"Parse total duration: {timer.Elapsed}");
            }
        }

        public static async Task ReloadConstsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                IComponentModel componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));

                if (ConstFinder.HasParsedSolution)
                {
                    var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;

                    var activeDocument = await SafeGetActiveDocumentAsync(dte);

                    if (activeDocument != null)
                    {
                        var workspace = (Workspace)componentModel.GetService<VisualStudioWorkspace>();
                        var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(activeDocument.FullName).FirstOrDefault();
                        if (documentId != null)
                        {
                            Document document = workspace.CurrentSolution.GetDocument(documentId);

                            await TrackConstsInDocumentAsync(document);
                        }
                    }
                }
                else
                {
                    await ConstFinder.TryParseSolutionAsync(componentModel);
                }
            }
            catch (Exception exc)
            {
                await OutputPane.Instance?.WriteAsync($"Error in {nameof(ReloadConstsAsync)}");
                ExceptionHelper.Log(exc);
            }
        }

        public static async Task<bool> TrackConstsInDocumentAsync(Document document)
        {
            if (document == null)
            {
                return false;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            System.Diagnostics.Debug.WriteLine(document.FilePath);

            if (document.FilePath == null
                || document.FilePath.Contains(".g.")
                || document.FilePath.Contains(".Designer."))
            {
                return false;
            }

            if (document.TryGetSyntaxTree(out SyntaxTree _))
            {
                var root = await document.GetSyntaxRootAsync();

                if (root == null)
                {
                    return false;
                }

                GetConstsFromSyntaxRoot(root, document.FilePath);
            }

            return true;
        }

        public static void GetConstsFromSyntaxRoot(SyntaxNode root, string filePath)
        {
            if (root == null || filePath == null)
            {
                return;
            }

            try
            {
                // Avoid parsing generated code.
                // Reduces overhead (as there may be lots)
                // Avoids assets included with Android projects.
                if (filePath.ToLowerInvariant().EndsWith(".designer.cs")
                 || filePath.ToLowerInvariant().EndsWith(".designer.vb")
                 || filePath.ToLowerInvariant().EndsWith(".g.cs")
                 || filePath.ToLowerInvariant().EndsWith(".g.i.cs"))
                {
                    return;
                }

                var toRemove = new List<(string, string, string, string)>();

                foreach (var item in KnownConsts)
                {
                    if (item.Source == filePath)
                    {
                        toRemove.Add(item);
                    }
                }

                foreach (var item in toRemove)
                {
                    KnownConsts.Remove(item);
                }

                if (ConstVisualizerPackage.Instance.Options.ProcessCSharpFiles)
                {
                    ExtractKnownCSharpConstants(root, filePath);
                }

                if (ConstVisualizerPackage.Instance.Options.ProcessVbFiles)
                {
                    ExtractKnownVisualBasicConstants(root, filePath);
                }
            }
            catch (Exception exc)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ExceptionHelper.Log(exc);
            }
        }

        public static bool IsConst(SyntaxNode node)
        {
            return node.ChildTokens().Any(t => t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConstKeyword) ||
                                               t.IsKind(Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.ConstKeyword));

            // The following will exclude all consts explicitly defined as private - May be better to track these so they can be lodaded within the same class
            ////&&
            ////!node.ChildTokens().Any(t => t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PrivateKeyword) ||
            ////                             t.IsKind(Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.PrivateKeyword));
        }

        internal static void AddToKnownConstants(string identifier, string qualifier, string value, string filePath)
        {
            if (value == null)
            {
                return;
            }

            var formattedValue = value.Replace("\\\"", "\"");

            if (formattedValue.StartsWith("nameof(", StringComparison.OrdinalIgnoreCase)
            && formattedValue.EndsWith(")"))
            {
                formattedValue = formattedValue.Substring(7, formattedValue.Length - 8);
            }

            KnownConsts.Add((identifier, qualifier, formattedValue, filePath));
        }

        internal static async Task<EnvDTE.Document> SafeGetActiveDocumentAsync(EnvDTE.DTE dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                // Some document types (inc. .csproj) throw an error when try and get the ActiveDocument
                // "The parameter is incorrect. (Exception from HRESULT: 0x80070057 (E_INVALIDARG))"
                EnvDTE.Document doc = await Task.FromResult(dte?.ActiveDocument);
                return doc != null && (doc.Language == "CSharp" || doc.Language == "Basic") ? doc : null;
            }
            catch (Exception exc)
            {
                // Don't call ExceptionHelper.Log as this is really common--see above
                ////await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ////ExceptionHelper.Log(exc);

                System.Diagnostics.Debug.WriteLine(exc);

#if DEBUG
                System.Diagnostics.Debugger.Break();
#endif
            }

            return null;
        }

        internal static void Reset()
        {
            KnownConsts.Clear();
            HasParsedSolution = false;
        }

        internal static string GetDisplayText(string constName, string qualifier, string fileName)
        {
            (_, _, var valueFromThisFile, _) =
                KnownConsts.Where(c => c.Source == fileName
                                    && c.Key == constName
                                    && c.Qualification.EndsWith(qualifier)).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(valueFromThisFile))
            {
                return valueFromThisFile;
            }

            (_, _, var valueFromOtherFile, _) =
                KnownConsts.Where(c => c.Key == constName
                                    && !string.IsNullOrEmpty(qualifier)
                                    && c.Qualification.EndsWith(qualifier)).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(valueFromOtherFile))
            {
                return valueFromOtherFile;
            }

            return string.Empty;
        }
    }
}