// <copyright file="ResourceAdornmentManager.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace ConstVisualizer
{
    /// <summary>
    /// Important class. Handles creation of adornments on appropriate lines.
    /// </summary>
    internal class ResourceAdornmentManager : IDisposable
    {
        private readonly IAdornmentLayer layer;
        private readonly IWpfTextView view;
        private readonly string fileName;
        private bool hasDoneInitialCreateVisualsPass = false;

        public ResourceAdornmentManager(IWpfTextView view)
        {
            this.view = view;
            this.layer = view.GetAdornmentLayer("ConstCommentLayer");

            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            this.fileName = this.GetFileName(view.TextBuffer);

            this.view.LayoutChanged += this.LayoutChangedHandler;
        }

        public static List<string> SearchValues { get; set; } = new List<string>();

        // Initialize to the same default as VS
        public static uint TextSize { get; set; } = 10;

        // Initialize to a reasonable value for display on light or dark themes/background.
        public static Color TextForegroundColor { get; set; } = Colors.Gray;

        public static string PreferredCulture { get; private set; }

        public static bool SupportAspNetLocalizer { get; private set; }

        public static bool SupportNamespaceAliases { get; private set; }

        // Keep a record of displayed text blocks so we can remove them as soon as changed or no longer appropriate
        // Also use this to identify lines to pad so the textblocks can be seen
        public Dictionary<int, List<(TextBlock textBlock, string resName)>> DisplayedTextBlocks { get; set; } = new Dictionary<int, List<(TextBlock textBlock, string resName)>>();

        public string GetFileName(ITextBuffer textBuffer)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var rc = textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDoc);

            if (rc == true)
            {
                return textDoc.FilePath;
            }
            else
            {
                rc = textBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer vsTextBuffer);

                if (rc)
                {
                    if (vsTextBuffer is IPersistFileFormat persistFileFormat)
                    {
                        persistFileFormat.GetCurFile(out string filePath, out _);
                        return filePath;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// This is called by the TextView when closing. Events are unsubscribed here.
        /// </summary>
        /// <remarks>
        /// It's actually called twice - once by the IPropertyOwner instance, and again by the ITagger instance.
        /// </remarks>
        public void Dispose() => this.UnsubscribeFromViewerEvents();

        /// <summary>
        /// On layout change add the adornment to any reformatted lines.
        /// </summary>
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void LayoutChangedHandler(object sender, TextViewLayoutChangedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            var collection = this.hasDoneInitialCreateVisualsPass ? (IEnumerable<ITextViewLine>)e.NewOrReformattedLines : this.view.TextViewLines;

            foreach (ITextViewLine line in collection)
            {
                int lineNumber = line.Snapshot.GetLineFromPosition(line.Start.Position).LineNumber;

                try
                {
                    await this.CreateVisualsAsync(line, lineNumber);
                }
                catch (InvalidOperationException ex)
                {
                    await OutputPane.Instance?.WriteAsync("Error handling layout changed");
                    await OutputPane.Instance?.WriteAsync(ex.Message);
                    await OutputPane.Instance?.WriteAsync(ex.Source);
                    await OutputPane.Instance?.WriteAsync(ex.StackTrace);
                }

                this.hasDoneInitialCreateVisualsPass = true;
            }
        }

        /// <summary>
        /// Scans text line for use of resource class, then adds new adornment.
        /// </summary>
        private async Task CreateVisualsAsync(ITextViewLine line, int lineNumber)
        {
            try
            {
                if (!ConstFinder.KnownConsts.Any())
                {
                    // If there are no known resource files then there's no point doing anything that follows.
                    return;
                }

                string lineText = line.Extent.GetText();

                // Don't add adornment to the definitions
                if (lineText.Contains(" const "))
                {
                    return;
                }

                // The extent will include all of a collapsed section
                if (lineText.Contains(Environment.NewLine))
                {
                    // We only want the first "line" here as that's all that can be seen on screen
                    lineText = lineText.Substring(0, lineText.IndexOf(Environment.NewLine, StringComparison.InvariantCultureIgnoreCase));
                }

                string[] searchArray = ConstFinder.SearchValues;

                // Remove any textblocks displayed on this line so it won't conflict with anything we add below.
                // Handles no textblocks to show or the text to display having changed.
                if (this.DisplayedTextBlocks.ContainsKey(lineNumber))
                {
                    foreach (var (textBlock, _) in this.DisplayedTextBlocks[lineNumber])
                    {
                        this.layer.RemoveAdornment(textBlock);
                    }

                    this.DisplayedTextBlocks.Remove(lineNumber);
                }

                var matches = await lineText.GetAllWholeWordIndexesAsync(searchArray);

                if (matches.Any())
                {
                    var lastLeft = double.NaN;

                    // Reverse the list to can go through them right-to-left so know if there's anything that might overlap
                    matches.Reverse();

                    foreach (var (index, value) in matches)
                    {
                        var qualNameStart = lineText.Substring(0, index).LastIndexOfAny(new[] { ' ', ':', ',', '"', '(', ')', '{', '}', '[', ']' });

                        var qualifier = lineText.Substring(qualNameStart + 1, index - qualNameStart - 1);

                        var displayText = ConstFinder.GetDisplayText(value, qualifier.TrimEnd('.'), this.fileName);

                        if (string.IsNullOrWhiteSpace(displayText))
                        {
                            break;
                        }

                        // Don't adorn a method that has the same name as a const
                        if (lineText.Substring(index + value.Length, 2) == "()")
                        {
                            break;
                        }

                        // Don't adorn a part of a literal string that matches a const
                        if (lineText[index - 1] == '"' || lineText[index + value.Length] == '"')
                        {
                            break;
                        }

                        if (!this.DisplayedTextBlocks.ContainsKey(lineNumber))
                        {
                            this.DisplayedTextBlocks.Add(lineNumber, new List<(TextBlock textBlock, string resName)>());
                        }

                        if (!string.IsNullOrWhiteSpace(displayText) && TextSize > 0)
                        {
                            var brush = new SolidColorBrush(TextForegroundColor);
                            brush.Freeze();

                            const double textBlockSizeToFontScaleFactor = 1.4;

                            var tb = new TextBlock
                            {
                                Foreground = brush,
                                Text = displayText,
                                FontSize = TextSize,
                                Height = TextSize * textBlockSizeToFontScaleFactor,
                            };

                            this.DisplayedTextBlocks[lineNumber].Add((tb, value));

                            // Get coordinates of text
                            int start = line.Extent.Start.Position + index;
                            int end = line.Start + (line.Extent.Length - 1);
                            var span = new SnapshotSpan(this.view.TextSnapshot, Span.FromBounds(start, end));
                            var lineGeometry = this.view.TextViewLines.GetMarkerGeometry(span);

                            if (!double.IsNaN(lastLeft))
                            {
                                tb.MaxWidth = lastLeft - lineGeometry.Bounds.Left - 5; // Minus 5 for padding
                                tb.TextTrimming = TextTrimming.CharacterEllipsis;
                            }

                            Canvas.SetLeft(tb, lineGeometry.Bounds.Left);
                            Canvas.SetTop(tb, line.TextTop - tb.Height);

                            lastLeft = lineGeometry.Bounds.Left;

                            this.layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, line.Extent, tag: null, adornment: tb, removedCallback: null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await OutputPane.Instance?.WriteAsync("Error creating visuals");
                await OutputPane.Instance?.WriteAsync(ex.Message);
                await OutputPane.Instance?.WriteAsync(ex.Source);
                await OutputPane.Instance?.WriteAsync(ex.StackTrace);
            }
        }

        private void UnsubscribeFromViewerEvents()
        {
            this.view.LayoutChanged -= this.LayoutChangedHandler;
        }
    }
}
