// <copyright file="ConstVisualizerPackage.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace ConstVisualizer
{
    [ProvideAutoLoad(UIContextGuids.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.7")] // Info on this package for Help/About
    [Guid(ConstVisualizerPackage.PackageGuidString)]
    public sealed class ConstVisualizerPackage : AsyncPackage
    {
        public const string PackageGuidString = "3bc35430-0b58-47c6-bcc4-96411b5c41e8";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await this.LoadSystemTextSettingsAsync(cancellationToken);

            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterOpenSolution += this.HandleOpenSolution;
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += this.HandleCloseSolution;

            await this.SetUpRunningDocumentTableEventsAsync(cancellationToken).ConfigureAwait(false);

            var componentModel = GetGlobalService(typeof(SComponentModel)) as IComponentModel;

            await ConstFinder.TryParseSolutionAsync(componentModel);

            VSColorTheme.ThemeChanged += (e) => this.LoadSystemTextSettingsAsync(CancellationToken.None).LogAndForget(nameof(ConstVisualizerPackage));
        }

        private void HandleOpenSolution(object sender, EventArgs e)
        {
            this.JoinableTaskFactory.RunAsync(() => this.HandleOpenSolutionAsync(this.DisposalToken)).Task.LogAndForget(nameof(this.HandleOpenSolutionAsync));
        }

        private async Task HandleOpenSolutionAsync(CancellationToken cancellationToken)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!ConstFinder.HasParsedSolution)
            {
                await ConstFinder.TryParseSolutionAsync();
            }
        }

        private void HandleCloseSolution(object sender, EventArgs e)
        {
            ConstFinder.Reset();
        }

        private async Task SetUpRunningDocumentTableEventsAsync(CancellationToken cancellationToken)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var runningDocumentTable = new RunningDocumentTable(this);

            runningDocumentTable.Advise(MyRunningDocTableEvents.Instance);
        }

        private async Task LoadSystemTextSettingsAsync(CancellationToken cancellationToken)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                IVsFontAndColorStorage storage = (IVsFontAndColorStorage)GetGlobalService(typeof(IVsFontAndColorStorage));

                var guid = new Guid("A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0");

                if (storage != null && storage.OpenCategory(ref guid, (uint)(__FCSTORAGEFLAGS.FCSF_READONLY | __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS)) == Microsoft.VisualStudio.VSConstants.S_OK)
                {
#pragma warning disable SA1129 // Do not use default value type constructor
                    LOGFONTW[] fnt = new LOGFONTW[] { new LOGFONTW() };
                    FontInfo[] info = new FontInfo[] { new FontInfo() };
#pragma warning restore SA1129 // Do not use default value type constructor

                    if (storage.GetFont(fnt, info) == Microsoft.VisualStudio.VSConstants.S_OK)
                    {
                        var fontSize = info[0].wPointSize;

                        if (fontSize > 0)
                        {
                            ResourceAdornmentManager.TextSize = fontSize;
                        }
                    }
                }

                if (storage != null && storage.OpenCategory(ref guid, (uint)(__FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS | __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS)) == Microsoft.VisualStudio.VSConstants.S_OK)
                {
                    var info = new ColorableItemInfo[1];

                    // Get the color value configured for regular string display
                    if (storage.GetItem("String", info) == Microsoft.VisualStudio.VSConstants.S_OK)
                    {
                        var win32Color = (int)info[0].crForeground;

                        int r = win32Color & 0x000000FF;
                        int g = (win32Color & 0x0000FF00) >> 8;
                        int b = (win32Color & 0x00FF0000) >> 16;

                        var textColor = Color.FromRgb((byte)r, (byte)g, (byte)b);

                        ResourceAdornmentManager.TextForegroundColor = textColor;
                    }
                }
            }
            catch (Exception exc)
            {
                await OutputPane.Instance?.WriteAsync("Error in LoadSystemTextSettingsAsync");
                await OutputPane.Instance?.WriteAsync(exc.Message);
                await OutputPane.Instance?.WriteAsync(exc.Source);
                await OutputPane.Instance?.WriteAsync(exc.StackTrace);
            }
        }
    }
}
