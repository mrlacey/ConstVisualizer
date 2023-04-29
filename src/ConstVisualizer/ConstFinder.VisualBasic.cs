// <copyright file="ConstFinder.VisualBasic.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace ConstVisualizer
{
    internal static partial class ConstFinder
    {
        public static string GetQualificationVisualBasic(VisualBasicSyntaxNode dec)
        {
            var result = string.Empty;
            SyntaxNode parent = dec.Parent;

            while (parent != null)
            {
                if (parent is TypeBlockSyntax tbs)
                {
                    result = $"{tbs.BlockStatement.Identifier.ValueText}.{result}";
                    parent = tbs.Parent;
                }
                else if (parent is NamespaceBlockSyntax nbs)
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

        private static void ExtractKnownVisualBasicConstants(SyntaxNode root, string filePath)
        {
            foreach (var vdec in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (vdec != null)
                {
                    if (vdec.Parent != null && vdec.Parent is DeclarationStatementSyntax dec)
                    {
                        if (IsConst(dec))
                        {
                            if (dec is FieldDeclarationSyntax fds)
                            {
                                var qualification = GetQualificationVisualBasic(fds);

                                foreach (VariableDeclaratorSyntax variable in fds.Declarators)
                                {
                                    foreach (ModifiedIdentifierSyntax name in variable.Names)
                                    {
                                        AddToKnownConstants(
                                            name.Identifier.Text,
                                            qualification,
                                            variable.Initializer?.Value?.ToString(),
                                            filePath);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (vdec.Parent != null && vdec.Parent is LocalDeclarationStatementSyntax ldec)
                        {
                            if (IsConst(ldec))
                            {
                                if (vdec is VariableDeclaratorSyntax vds)
                                {
                                    var qualification = GetQualificationVisualBasic(vds);
                                    foreach (ModifiedIdentifierSyntax name in vds.Names)
                                    {
                                        AddToKnownConstants(
                                            name.Identifier.Text,
                                            qualification,
                                            vds.Initializer?.Value?.ToString(),
                                            filePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
