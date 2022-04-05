﻿// <copyright file="StringExtensions.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConstVisualizer
{
    public static class StringExtensions
    {
        public static async Task<(int index, string value, bool retry)> IndexOfAnyAsync(this string source, params string[] values)
        {
            try
            {
                var valuePositions = new Dictionary<string, int>();

                // Values may be duplicated if multiple apps in the project have resources with the same name.
                foreach (var value in values.Distinct())
                {
                    valuePositions.Add(value, source.IndexOf(value));
                }

                if (valuePositions.Any(v => v.Value > -1))
                {
                    var found = valuePositions.Where(v => v.Value > -1)
                                              .OrderBy(v => v.Value)
                                              .ToList();

                    if (found.Any())
                    {
                        var result = found.Where(f => f.Value == found.First().Value)
                                          .OrderBy(v => v.Key.Length)
                                          .First();

                        return (result.Value, result.Key, found.Count(c => c.Value == result.Value) > 1);
                    }
                    else
                    {
                        return (-1, string.Empty, false);
                    }
                }
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await OutputPane.Instance?.WriteAsync("Error in IndexOfAnyAsync");
                await OutputPane.Instance?.WriteAsync(source);
                await OutputPane.Instance?.WriteAsync(string.Join("|", values));
                ExceptionHelper.Log(ex);
            }

            return (-1, string.Empty, false);
        }

        public static async Task<List<(int index, string value)>> GetAllWholeWordIndexesAsync(this string source, params string[] values)
        {
            var result = new List<(int, string)>();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var startPos = 0;

                var ignoreInRetries = new List<string>();

                while (startPos > -1 && startPos <= source.Length)
                {
                    var toSearchFor = values.Where(v => !ignoreInRetries.Contains(v)).ToArray();

                    var (index, value, retry) = await source.Substring(startPos)
                                                            .IndexOfAnyAsync(toSearchFor);

                    if (index > -1)
                    {
                        var prevChar = source[startPos + index - 1];

                        // Account for matching text being at the end of the line.
                        //  It won't be in normal use but could be in comments.
                        var nextCharPos = startPos + index + value.Length;
                        char nextChar = ' ';

                        if (nextCharPos < source.Length)
                        {
                            nextChar = source[nextCharPos];
                        }

                        if (await source.IsValidVariableNameAsync(prevChar, nextChar))
                        {
                            result.Add((startPos + index, value));
                        }

                        if (retry)
                        {
                            ignoreInRetries.Add(value);
                        }
                        else
                        {
                            ignoreInRetries.Clear();
                            startPos = startPos + index + 1;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await OutputPane.Instance?.WriteAsync("Error in GetAllWholeWordIndexesAsync");
                await OutputPane.Instance?.WriteAsync(source);
                await OutputPane.Instance?.WriteAsync(string.Join("|", values));
                ExceptionHelper.Log(ex);
            }

            return result;
        }

        /// <summary>
        /// Given a string, by looking at the characters either side of it, could it be a valid variable name
        /// Valid names
        /// - start with @, _, or letter
        /// - other characters are: letter, digit, or underscore.
        /// </summary>
        public static async Task<bool> IsValidVariableNameAsync(this string source, char charBefore, char charAfter)
        {
            try
            {
                if (char.IsLetterOrDigit(charBefore))
                {
                    return false;
                }
                else if (charBefore == '_')
                {
                    return false;
                }
                else if (charBefore == '@')
                {
                    return false;
                }

                if (char.IsLetterOrDigit(charAfter))
                {
                    return false;
                }
                else if (charAfter == '_')
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await OutputPane.Instance?.WriteAsync("Error in GetAllIndexesCaseInsensitiveAsync");
                await OutputPane.Instance?.WriteAsync(source);
                await OutputPane.Instance?.WriteAsync(charBefore.ToString());
                await OutputPane.Instance?.WriteAsync(charAfter.ToString());
                ExceptionHelper.Log(ex);

                return false;
            }
        }

        public static async Task<List<int>> GetAllIndexesCaseInsensitiveAsync(this string source, string searchTerm)
        {
            var result = new List<int>();

            try
            {
                var startPos = 0;

                while (startPos > -1 && startPos <= source.Length)
                {
                    var index = source.Substring(startPos).IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase);

                    if (index > -1)
                    {
                        result.Add(startPos + index);
                        startPos = startPos + index + 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await OutputPane.Instance?.WriteAsync("Error in GetAllIndexesCaseInsensitiveAsync");
                await OutputPane.Instance?.WriteAsync(source);
                await OutputPane.Instance?.WriteAsync(searchTerm);
                ExceptionHelper.Log(ex);
            }

            return result;
        }
    }
}
