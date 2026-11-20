using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace DS4WindowsTests;

/// <summary>
/// Cancel, Save-and-close and preset apply all detach the editor pages from their
/// view models by clearing DataContext (ProfileEditor.StopEditorBindings). WPF then
/// walks the page tree to push the inherited change down, and a property row whose
/// Header changes during that walk throws "a tree walk is in progress". These checks
/// run the same detach on the real markup with a disposable fixture.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ProfileEditorTeardownTests
{
    private static readonly XNamespace X = PresentationHarness.X;

    [TestMethod]
    public void DetachingTheEditorPagesAfterVisitingThemDoesNotThrow()
    {
        foreach (bool optionsOn in new[] { false, true })
            WpfTestHost.Run(() =>
            {
                ProfileWorkspacePresentationTests.Theme(false);
                UserControl view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
                var owner = new Window { Content = view };
                try
                {
                    var settings = (TabControl)view.FindName("profileSettingsTabCon");
                    var mappings = (FrameworkElement)view.FindName("mappingListBox");
                    var actions = (FrameworkElement)view.FindName("specialActionsTab");
                    var lightbar = (FrameworkElement)view.FindName("lightbarRect");
                    object fixture = ProfilePropertyPageFixture.Create(optionsOn);

                    // RefreshEditorBindings: the pages get their view model.
                    settings.DataContext = fixture;
                    mappings.DataContext = fixture;
                    actions.DataContext = fixture;
                    lightbar.DataContext = fixture;
                    for (int section = 0; section < 8; section++)
                    {
                        ProfilePropertyPageFixture.ShowSection(view, section);
                        PresentationHarness.Layout(view, new Size(1024, 660));
                    }

                    // StopEditorBindings, as Cancel, Save-and-close and preset apply run it.
                    settings.DataContext = null;
                    mappings.DataContext = null;
                    actions.DataContext = null;
                    lightbar.DataContext = null;

                    // Preset apply then binds the same pages again, and closing detaches them once more.
                    settings.DataContext = fixture;
                    PresentationHarness.Layout(view, new Size(1024, 660));
                    settings.DataContext = null;
                }
                finally
                {
                    owner.Content = null;
                    owner.Close();
                }
            });
    }

    /// <summary>
    /// The structural rule behind the check above: a row label may follow another
    /// control's text, but only text that is fixed in markup, never text that comes
    /// from the profile data and changes while the editor detaches.
    /// </summary>
    [TestMethod]
    public void RowHeadersDoNotFollowDataBoundText()
    {
        foreach (string file in new[] { "ProfileEditor", "AxialStickUserControl", "TouchButtonUserControl" })
        {
            XElement source = PresentationHarness.Source(file);
            var byName = source.Descendants()
                .Where(e => e.Attribute(X + "Name") != null)
                .ToDictionary(e => (string)e.Attribute(X + "Name"));
            foreach (XElement row in source.Descendants().Where(e => e.Name.LocalName == "HeaderedContentControl"))
            {
                string header = (string)row.Attribute("Header") ?? "";
                Assert.IsFalse(IsDataBinding(header), $"{file}: row header '{header}' is bound to profile data.");
                Match link = Regex.Match(header, @"\{Binding\s+(?:Path=)?(\w+),\s*ElementName=(\w+)\}");
                if (!link.Success) continue;
                Assert.IsTrue(byName.TryGetValue(link.Groups[2].Value, out XElement target), $"{file}: '{header}' names a missing element.");
                string text = (string)target.Attribute(link.Groups[1].Value) ?? "";
                Assert.IsFalse(IsDataBinding(text),
                    $"{file}: row header follows {link.Groups[2].Value}.{link.Groups[1].Value} = '{text}', which is bound to profile data.");
            }
        }
    }

    private static bool IsDataBinding(string value) =>
        value.StartsWith("{Binding") && !Regex.IsMatch(value, "ElementName|RelativeSource|Source=");
}
