// <copyright file="OptionsGrid.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
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
    }
}
