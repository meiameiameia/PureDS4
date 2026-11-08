using DS4Windows;
using DS4WinWPF.DS4Forms;
using System.Windows;
using System.Windows.Controls;

namespace DS4WindowsTests;

[TestClass]
[DoNotParallelize]
public class MigrationDialogPresentationTests
{
    internal static readonly string[] States = {
        "import-empty", "import-profiles", "import-results", "removal-empty",
        "removal-ready", "removal-blocked", "removal-residual", "running",
        "running-recheck", "storage-fresh", "storage-existing", "storage-protected",
    };

    [TestMethod]
    public void MigrationStatesKeepChoicesVisibleAndContentReachable()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (string state in States.Where(s => !s.StartsWith("storage")))
            foreach (double height in new[] { 244d, 404d })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateView(state);
                double width = state.StartsWith("removal") ? 544 : 504;
                Size size = new(width, height);
                PresentationHarness.Layout(view, size);
                var scroll = (ScrollViewer)view.FindName("migrationScroll");
                var footer = (Border)view.FindName("migrationActions");
                Rect before = Bounds(view, footer);
                foreach (Button button in PresentationHarness.Descendants<Button>(footer).Where(PresentationHarness.Participates))
                {
                    Inside(view, button);
                    Assert.IsFalse(button.IsDefault, "Styling must not add implicit Enter confirmation.");
                    Assert.IsFalse(button.IsCancel, "Existing click/close semantics must be retained.");
                }
                Assert.AreEqual(0d, scroll.ScrollableWidth, 1);
                foreach (string name in new[] { "headingText", "summaryText", "explanationText", "resultText", "preservationText", "blockedText", "statusText" })
                    if (view.FindName(name) is FrameworkElement element && PresentationHarness.Participates(element))
                        PresentationHarness.AssertReachable(view, element, state);
                scroll.ScrollToEnd(); PresentationHarness.Layout(view, size);
                Assert.AreEqual(before, Bounds(view, footer), state + ": actions moved when scrolling.");
                foreach (ItemsControl list in PresentationHarness.Descendants<ItemsControl>(view))
                    if (PresentationHarness.Participates(list))
                        PresentationHarness.AssertReachable(view, list, state);
                if (state == "removal-blocked") Assert.IsFalse(((Button)view.FindName("uninstallButton")).IsEnabled);
                if (state is "removal-empty" or "removal-residual")
                    Assert.AreEqual(Visibility.Collapsed, ((Button)view.FindName("uninstallButton")).Visibility);
                if (state is "import-empty" or "import-results") Assert.IsFalse(((Button)view.FindName("importButton")).IsEnabled);
            }
        });
    }

    [TestMethod]
    public void ImportResultsWrapAndRemainScrollableToTheLastProfile()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateView("import-results");
                var list = (ListBox)view.FindName("candidateList");
                list.ItemsSource = Enumerable.Range(1, 80).Select(i =>
                    "Disposable profile " + i + " with a deliberately long name and import destination (DS4Windows).xml - Already imported").ToArray();
                Size size = new(504, 404);
                PresentationHarness.Layout(view, size);
                var outer = (ScrollViewer)view.FindName("migrationScroll");
                outer.ScrollToEnd(); PresentationHarness.Layout(view, size);
                list.ScrollIntoView(list.Items[list.Items.Count - 1]);
                PresentationHarness.Layout(view, size);
                var last = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1);
                Assert.IsNotNull(last, "The last profile must be realized after scrolling to it.");
                foreach (ScrollViewer scroll in PresentationHarness.Descendants<ScrollViewer>(view))
                    Assert.AreEqual(0d, scroll.ScrollableWidth, 1,
                        $"Profile names must wrap: {scroll.Name}, viewport={scroll.ViewportWidth}, extent={scroll.ExtentWidth}, horizontal={scroll.HorizontalScrollBarVisibility}, contentScroll={scroll.CanContentScroll}.");
                var inner = PresentationHarness.Descendants<ScrollViewer>(list).Single();
                Inside(inner, last);
                Assert.IsTrue(inner.CanContentScroll, "The list must retain its virtualizing scroll host.");
                var text = PresentationHarness.Descendants<TextBlock>(last).Single();
                Assert.IsTrue(text.ActualHeight > 20, "The long result must wrap to multiple lines.");
                Assert.IsTrue(VirtualizingPanel.GetIsVirtualizing(list));
                Assert.AreEqual(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(list));
                Assert.IsTrue(PresentationHarness.Descendants<ListBoxItem>(list).Count() < list.Items.Count,
                    "A long import list must not realize every item at once.");
            }
        });
    }

    [TestMethod]
    public void StorageChoicesKeepDescriptionsAndPermissionStateWithoutActivatingAStore()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (string state in States.Where(s => s.StartsWith("storage")))
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateView(state);
                Size size = new(504, 244);
                PresentationHarness.Layout(view, size);
                var scroll = (ScrollViewer)view.FindName("storageScroll");
                Assert.AreEqual(0d, scroll.ScrollableWidth, 1);
                foreach (string name in new[] { "headingText", "summaryText", "accountButton", "accountDescriptionText", "portableButton", "portableDescriptionText" })
                    PresentationHarness.AssertReachable(view, (FrameworkElement)view.FindName(name), state);
                Assert.AreEqual(state != "storage-protected", ((Button)view.FindName("portableButton")).IsEnabled);
                scroll.ScrollToEnd(); PresentationHarness.Layout(view, size);
                Inside(scroll, (TextBlock)view.FindName("portableDescriptionText"));
                foreach (Button choice in PresentationHarness.Descendants<Button>(view)) Assert.IsFalse(choice.IsDefault);
            }
        });
    }

    internal static string SourceName(string state) => state.StartsWith("import") ? "LegacyProfileImportWindow" :
        state.StartsWith("removal") ? "LegacyRemovalPlanWindow" : state.StartsWith("storage") ? "SaveWhere" : "LegacyApplicationRunningWindow";

    // Actual markup, detached events, pure presentation helpers, fictitious
    // survey/result objects. Never construct these product windows: their
    // constructors may scan the host or check the live application's folder.
    internal static UserControl CreateView(string state)
    {
        UserControl view = PresentationHarness.LoadControl(SourceName(state));
        void Text(string name, string value) => ((TextBlock)view.FindName(name)).Text = value;
        void Visible(string name, bool visible) => ((UIElement)view.FindName(name)).Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (state.StartsWith("import"))
        {
            var candidates = state == "import-empty" ? Array.Empty<LegacyProfileImportCandidate>() : new[] {
                new LegacyProfileImportCandidate("Default.xml", "not-a-source"),
                new LegacyProfileImportCandidate("A long racing game profile with custom steering and button mappings.xml", "not-a-source"),
                new LegacyProfileImportCandidate("Strategy.xml", "not-a-source"),
            };
            var survey = new LegacyProfileImportSurvey(candidates.Length > 0, candidates);
            Text("headingText", "Import profiles from DS4Windows");
            Text("summaryText", LegacyProfileImportWindow.BuildSummary(survey));
            Visible("candidateListBorder", survey.HasImportableProfiles);
            ((ListBox)view.FindName("candidateList")).ItemsSource = candidates.Select(c => c.FileName).ToArray();
            ((Button)view.FindName("importButton")).IsEnabled = survey.HasImportableProfiles;
            if (state == "import-results")
            {
                var results = new[] {
                    new LegacyProfileImportResult("Default.xml", LegacyProfileImportOutcome.Imported, destinationFileName: "Default (DS4Windows).xml"),
                    new LegacyProfileImportResult(candidates[1].FileName, LegacyProfileImportOutcome.SkippedAlreadyExists, destinationFileName: candidates[1].FileName),
                    new LegacyProfileImportResult("Strategy.xml", LegacyProfileImportOutcome.Failed, "Disposable failure"),
                };
                ((ListBox)view.FindName("candidateList")).ItemsSource = results.Select(LegacyProfileImportWindow.DescribeResult).ToArray();
                Text("resultText", LegacyProfileImportWindow.BuildResultSummary(results));
                Visible("resultText", true);
                ((Button)view.FindName("importButton")).IsEnabled = false;
            }
        }
        else if (state.StartsWith("removal"))
        {
            bool empty = state == "removal-empty", residual = state == "removal-residual", blocked = state == "removal-blocked";
            var items = empty ? Array.Empty<LegacyRemovalItem>() : residual ? new[] {
                new LegacyRemovalItem(LegacyRemovalItemCategory.Configuration, "Preserved profiles and settings", @"C:\Example\Preserved configuration"),
            } : new[] {
                new LegacyRemovalItem(LegacyRemovalItemCategory.ApplicationUninstaller, "Windows uninstaller", "An existing registered uninstall command"),
                new LegacyRemovalItem(LegacyRemovalItemCategory.InstallDirectory, "Installation folder", @"C:\Example\A longer legacy application installation folder\DS4Windows"),
                new LegacyRemovalItem(LegacyRemovalItemCategory.ScheduledTask, "Startup entry", "A detected legacy entry; this preview does not access or change it"),
            };
            var plan = new LegacyRemovalPlan(items, !empty, !empty && !residual);
            Text("headingText", blocked ? "Finish replacing DS4Windows" : "DS4Windows removal plan");
            Text("summaryText", LegacyRemovalPlanWindow.BuildSummary(plan));
            ((ItemsControl)view.FindName("itemList")).ItemsSource = items.Select(i => i.Description + ": " + i.Detail).ToArray();
            Visible("itemListBorder", plan.HasDetectedState);
            Visible("preservationText", plan.ConfigurationWouldSurvive);
            Text("preservationText", "Your DS4Windows profiles and settings are not part of this plan. Windows application uninstallers do not normally delete a per-user AppData folder, and PureDS4 does not either - removing that data, if you ever want to, is a separate, manual decision.");
            Visible("uninstallButton", plan.HasAnythingToRemove);
            Visible("recheckButton", plan.HasAnythingToRemove);
            ((Button)view.FindName("uninstallButton")).IsEnabled = !blocked;
            Visible("blockedText", blocked);
            Text("blockedText", LegacyRemovalPlanWindow.DescribeBlocker(blocked ? LegacyUninstallReadiness.ApplicationRunning : LegacyUninstallReadiness.Ready));
            if (blocked) ((Button)view.FindName("closeButton")).Content = "Exit PureDS4";
        }
        else if (state.StartsWith("storage"))
        {
            FirstRunStoragePresentation copy = SaveWhere.CreatePresentation(state == "storage-existing");
            Text("headingText", copy.Heading); Text("summaryText", copy.Summary);
            Text("existingDataText", copy.ExistingDataNotice); Visible("existingDataText", copy.ShowExistingDataNotice);
            ((Button)view.FindName("accountButton")).Content = copy.AccountAction;
            ((Button)view.FindName("portableButton")).Content = copy.PortableAction;
            Text("accountDescriptionText", copy.AccountDescription); Text("portableDescriptionText", copy.PortableDescription);
            if (state == "storage-protected")
            {
                ((UIElement)view.FindName("portablePanel")).IsEnabled = false;
                Text("portableDescriptionText", "Not available here: this folder is not writable without administrator rights.");
            }
        }
        else
        {
            var copy = LegacyApplicationRunningWindow.CreatePresentation("DS4Windows Reworked");
            Text("headingText", copy.Heading); Text("explanationText", copy.Explanation);
            Visible("statusText", state == "running-recheck");
            Text("statusText", "PureDS4 checked again and DS4Windows Reworked is still running.");
        }
        return view;
    }

    private static Rect Bounds(FrameworkElement root, FrameworkElement child) =>
        child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));

    private static void Inside(FrameworkElement root, FrameworkElement child)
    {
        Rect b = Bounds(root, child);
        Assert.IsTrue(b.Left >= -1 && b.Right <= root.ActualWidth + 1 && b.Top >= -1 && b.Bottom <= root.ActualHeight + 1,
            child.Name + ": " + b + " outside " + root.RenderSize);
    }
}
