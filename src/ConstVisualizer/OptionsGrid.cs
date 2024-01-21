// <copyright file="OptionsGrid.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace ConstVisualizer
{
    public class OptionsGrid : DialogPage
    {
        [Category("Alignment")]
        [DisplayName("Bottom padding")]
        [Description("Pixels to add below the displayed value.")]
        public int BottomPadding { get; set; } = 0;

        [Category("Alignment")]
        [DisplayName("Top padding")]
        [Description("Pixels to add above the displayed value.")]
        public int TopPadding { get; set; } = 1;

        [Category("Language")]
        [DisplayName("C Sharp")]
        [Description("Support C# files.")]
        public bool ProcessCSharpFiles { get; set; } = true;

        [Category("Language")]
        [DisplayName("Visual Basic")]
        [Description("Process VB.Net files.")]
        public bool ProcessVbFiles { get; set; } = false;

        [Category("Advanced")]
        [DisplayName("Enable Advanced Logging")]
        [Description("Enable advanced logging.")]
        public bool AdvancedLogging { get; set; } = false;
    }
}
