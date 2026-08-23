/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using DS4Windows;
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            versionText.Text = $"Version {Global.exeDisplayVersion}";
        }

        private void SourceLink_Click(object sender, RoutedEventArgs e) =>
            Util.StartProcessHelper(ProductIdentity.RepositoryUrl);

        private void UpstreamLink_Click(object sender, RoutedEventArgs e) =>
            Util.StartProcessHelper("https://github.com/hbashton/DS4Windows");

        private void NoticesLink_Click(object sender, RoutedEventArgs e) =>
            Util.StartProcessHelper(ProductIdentity.RepositoryUrl + "/blob/main/NOTICE.txt");
    }
}
