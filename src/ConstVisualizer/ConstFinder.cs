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
using CsharpCodeAnalysis = Microsoft.CodeAnalysis.CSharp;
using CsharpSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;
using Task = System.Threading.Tasks.Task;
using VBasicCodeAnalysis = Microsoft.CodeAnalysis.VisualBasic;
using VBasicSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace ConstVisualizer
{
    internal static class ConstFinder
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

                ////OutputPane.Instance.WriteLine($"Parse step 1 duration: {timer.Elapsed}");

                var workspace = (Workspace)componentModel.GetService<VisualStudioWorkspace>();

                if (workspace == null)
                {
                    return;
                }

                ////OutputPane.Instance.WriteLine($"Parse step 2 duration: {timer.Elapsed}");

                var projectGraph = workspace.CurrentSolution?.GetProjectDependencyGraph();

                if (projectGraph == null)
                {
                    return;
                }

                ////OutputPane.Instance.WriteLine($"Parse step 3 duration: {timer.Elapsed}");

                await Task.Yield();

                var projects = projectGraph.GetTopologicallySortedProjects();

                ////OutputPane.Instance.WriteLine($"Parse step 4 duration: {timer.Elapsed}");

                foreach (ProjectId projectId in projects)
                {
                    var projectCompilation = await workspace.CurrentSolution?.GetProject(projectId).GetCompilationAsync();

                    ////OutputPane.Instance.WriteLine($"Parse loop step duration: {timer.Elapsed} ({projectId})");

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

                void AddToKnownConstants(string identifier, string qualifier, string value)
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

                foreach (CsharpSyntax.VariableDeclarationSyntax vdec in root.DescendantNodes().OfType<CsharpSyntax.VariableDeclarationSyntax>())
                {
                    if (vdec != null)
                    {
                        if (vdec.Parent != null && vdec.Parent is CsharpSyntax.MemberDeclarationSyntax dec)
                        {
                            if (IsConst(dec))
                            {
                                if (dec is CsharpSyntax.FieldDeclarationSyntax fds)
                                {
                                    var qualification = GetQualificationCSharp(fds);

                                    foreach (CsharpSyntax.VariableDeclaratorSyntax variable in fds.Declaration?.Variables)
                                    {
                                        AddToKnownConstants(
                                            variable.Identifier.Text,
                                            qualification,
                                            variable.Initializer?.Value?.ToString());
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (vdec.Parent != null && vdec.Parent is CsharpSyntax.LocalDeclarationStatementSyntax ldec)
                            {
                                if (IsConst(ldec))
                                {
                                    if (vdec is CsharpSyntax.VariableDeclarationSyntax vds)
                                    {
                                        var qualification = GetQualificationCSharp(vds);

                                        foreach (CsharpSyntax.VariableDeclaratorSyntax variable in vds.Variables)
                                        {
                                            AddToKnownConstants(
                                                variable.Identifier.Text,
                                                qualification,
                                                variable.Initializer?.Value?.ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (VBasicSyntax.VariableDeclaratorSyntax vdec in root.DescendantNodes().OfType<VBasicSyntax.VariableDeclaratorSyntax>())
                {
                    if (vdec != null)
                    {
                        if (vdec.Parent != null && vdec.Parent is VBasicSyntax.DeclarationStatementSyntax dec)
                        {
                            if (IsConst(dec))
                            {
                                if (dec is VBasicSyntax.FieldDeclarationSyntax fds)
                                {
                                    var qualification = GetQualificationVisualBasic(fds);

                                    foreach (VBasicSyntax.VariableDeclaratorSyntax variable in fds.Declarators)
                                    {
                                        foreach (VBasicSyntax.ModifiedIdentifierSyntax name in variable.Names)
                                        {
                                            AddToKnownConstants(
                                                name.Identifier.Text,
                                                qualification,
                                                variable.Initializer?.Value?.ToString());
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (vdec.Parent != null && vdec.Parent is VBasicSyntax.LocalDeclarationStatementSyntax ldec)
                            {
                                if (IsConst(ldec))
                                {
                                    if (vdec is VBasicSyntax.VariableDeclaratorSyntax vds)
                                    {
                                        var qualification = GetQualificationVisualBasic(vds);
                                        foreach (VBasicSyntax.ModifiedIdentifierSyntax name in vds.Names)
                                        {
                                            AddToKnownConstants(
                                                name.Identifier.Text,
                                                qualification,
                                                vds.Initializer?.Value?.ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ExceptionHelper.Log(exc);
            }
        }

        public static string GetQualificationCSharp(CsharpCodeAnalysis.CSharpSyntaxNode dec)
        {
            var result = string.Empty;
            var parent = dec.Parent;

            while (parent != null)
            {
                if (parent is CsharpSyntax.TypeDeclarationSyntax tds)
                {
                    result = $"{tds.Identifier.ValueText}.{result}";
                    parent = tds.Parent;
                }
                else if (parent is CsharpSyntax.NamespaceDeclarationSyntax nds)
                {
                    result = $"{nds.Name}.{result}";
                    parent = nds.Parent;
                }
                else
                {
                    parent = parent.Parent;
                }
            }

            return result.TrimEnd('.');
        }

        public static string GetQualificationVisualBasic(VBasicCodeAnalysis.VisualBasicSyntaxNode dec)
        {
            var result = string.Empty;
            SyntaxNode parent = dec.Parent;

            while (parent != null)
            {
                if (parent is VBasicSyntax.TypeBlockSyntax tbs)
                {
                    result = $"{tbs.BlockStatement.Identifier.ValueText}.{result}";
                    parent = tbs.Parent;
                }
                else if (parent is VBasicSyntax.NamespaceBlockSyntax nbs)
                {
                    result = $"{nbs.NamespaceStatement.Name}.{result}";
                    parent = nbs.Parent;
                }
                else
                {
                    parent = parent.Parent;
                }
            }

            return result.TrimEnd('.');
        }

        public static bool IsConst(SyntaxNode node)
        {
            return node.ChildTokens().Any(t => t.IsKind(CsharpCodeAnalysis.SyntaxKind.ConstKeyword) ||
                                               t.IsKind(VBasicCodeAnalysis.SyntaxKind.ConstKeyword));
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
            var constsInThisFile =
                KnownConsts.Where(c => c.Source == fileName
                                    && c.Key == constName
                                    && c.Qualification.EndsWith(qualifier)).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(constsInThisFile.Value))
            {
                return constsInThisFile.Value;
            }

            (_, _, var value, _) =
                KnownConsts.Where(c => c.Key == constName
                                    && c.Qualification.EndsWith(qualifier)).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return string.Empty;
        }
    }
}