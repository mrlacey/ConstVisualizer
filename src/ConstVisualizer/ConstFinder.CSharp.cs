// <copyright file="ConstFinder.CSharp.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstVisualizer
{
	internal static partial class ConstFinder
	{
		public static string GetQualificationCSharp(CSharpSyntaxNode dec)
		{
			var result = string.Empty;
			var parent = dec.Parent;

			while (parent != null)
			{
				if (parent is TypeDeclarationSyntax tds)
				{
					result = $"{tds.Identifier.ValueText}.{result}";
					parent = tds.Parent;
				}
				else if (parent is NamespaceDeclarationSyntax nds)
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

		public static void ExtractKnownCSharpConstants(SyntaxNode root, string filePath)
		{
			foreach (var vdec in root.DescendantNodes().OfType<VariableDeclarationSyntax>())
			{
				if (vdec != null)
				{
					if (vdec.Parent != null && vdec.Parent is MemberDeclarationSyntax dec)
					{
						if (IsConst(dec))
						{
							if (dec is FieldDeclarationSyntax fds)
							{
								var qualification = GetQualificationCSharp(fds);

								foreach (VariableDeclaratorSyntax variable in fds.Declaration?.Variables)
								{
									AddToKnownConstants(
										variable.Identifier.Text,
										qualification,
										variable.Initializer?.Value?.ToString(),
										filePath);
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
								if (vdec is VariableDeclarationSyntax vds)
								{
									var qualification = GetQualificationCSharp(vds);

									foreach (var variable in vds.Variables)
									{
										AddToKnownConstants(
											variable.Identifier.Text,
											qualification,
											variable.Initializer?.Value?.ToString(),
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
