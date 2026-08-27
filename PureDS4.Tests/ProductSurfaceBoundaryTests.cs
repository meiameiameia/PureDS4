using DS4Windows;
using DS4WinWPF;
using DS4WinWPF.DS4Forms;
using DS4WinWPF.DS4Forms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace DS4WindowsTests
{
    /// <summary>
    /// Guards the boundary between what PureDS4 is and what it inherited.
    ///
    /// Two regressions keep reappearing while the derivative is narrowed. The
    /// first is branding drift: a window that still calls the product
    /// DS4Windows or DS4Windows Reworked, or points at a repository that no
    /// longer exists. The second is an unsupported-hardware control returning
    /// to ordinary or offline UI, which promises a DualSense, DualSense Edge,
    /// Switch Pro or Joy-Con feature this tool does not support.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ProductSurfaceBoundaryTests
    {
        private const string RetiredProductName = "DS4Windows Reworked";
        private const string RetiredRepository = "ds4windows-reworked";

        private static readonly string[] UnsupportedHardwareTriggerTags =
        {
            "Mute", "Capture", "SideL", "SideR",
            "Function Left", "Function Right",
            "Bottom Left Paddle", "Bottom Right Paddle",
        };

        private static readonly DS4Controls[] UnsupportedHardwareControls =
        {
            DS4Controls.Mute, DS4Controls.Capture, DS4Controls.SideL,
            DS4Controls.SideR, DS4Controls.FnL, DS4Controls.FnR,
            DS4Controls.BLP, DS4Controls.BRP,
        };

        [DataTestMethod]
        [DataRow((int)OutContType.ViiperDS4)]
        [DataRow((int)OutContType.ViiperX360)]
        public void UnsupportedControlsAreAbsentFromTheVisibleMappingList(
            int outputType)
        {
            // Disabling the row is not enough. A greyed-out "Function Left"
            // still tells the user this tool works with a DualSense Edge, so
            // the row must not be in the list the profile editor renders.
            MappingListViewModel mappingList = new MappingListViewModel(
                Global.TEST_PROFILE_INDEX, (OutContType)outputType);

            foreach (DS4Controls absent in UnsupportedHardwareControls)
            {
                Assert.IsFalse(
                    mappingList.Mappings.Any(
                        mapped => mapped.Control == absent),
                    $"{absent} is still rendered in the mapping list.");
                Assert.IsFalse(
                    mappingList.ControlIndexMap.ContainsKey(absent),
                    $"{absent} still occupies a visible mapping-list index.");

                // The saved binding is still reachable, so an imported profile
                // keeps it and writes it back unchanged.
                Assert.IsTrue(mappingList.ControlMap.ContainsKey(absent),
                    $"{absent} lost its preserved backend mapping.");
            }

            Assert.IsTrue(mappingList.Mappings.Count > 0);
            Assert.IsTrue(
                mappingList.Mappings.All(
                    mapped => mapped.IsAvailableOnPhysicalController),
                "Every rendered mapping row must be usable.");
        }

        [TestMethod]
        public void VisibleMappingIndexesStayAlignedWithTheRenderedList()
        {
            // The controller diagram selects rows by list index, so a gap in
            // the visible list must never leave an index pointing at the row
            // that shifted into its place.
            MappingListViewModel mappingList = new MappingListViewModel(
                Global.TEST_PROFILE_INDEX, OutContType.ViiperX360);

            foreach ((DS4Controls control, int index) in
                mappingList.ControlIndexMap)
            {
                Assert.IsTrue(index >= 0 &&
                    index < mappingList.Mappings.Count,
                    $"{control} maps outside the visible list.");
                Assert.AreEqual(control,
                    mappingList.Mappings[index].Control,
                    $"{control} does not sit at its mapped index.");
            }
        }

        [TestMethod]
        public void PublicOutputSurfaceOffersOnlyTheTwoSupportedOutputs()
        {
            // Switch 2 Pro was retired with the other controller families.
            Assert.AreEqual(OutContType.ViiperDS4,
                ProfileSettingsViewModel.GetOutputControllerType(0));
            Assert.AreEqual(OutContType.ViiperX360,
                ProfileSettingsViewModel.GetOutputControllerType(1));
            Assert.AreEqual(OutContType.ViiperX360,
                ProfileSettingsViewModel.GetOutputControllerType(2),
                "A stale third selection must fall back to a supported output.");

            foreach (OutContType supported in new[]
            {
                OutContType.ViiperDS4, OutContType.ViiperX360,
            })
            {
                Assert.AreNotEqual("None", supported.ToDisplayName());
            }

            // A stale stored value must present the supported output it
            // actually becomes, never a retired persona name.
            Assert.AreEqual("Xbox 360",
                OutContType.ViiperSwitch2Pro.ToDisplayName());
            Assert.AreEqual("DualShock 4",
                OutContType.ViiperDualSense.ToDisplayName());
            foreach (OutContType retired in new[]
            {
                OutContType.ViiperSwitch2Pro, OutContType.ViiperDualSense,
                OutContType.ViiperDualSenseEdge,
            })
            {
                Assert.AreNotEqual(retired, retired.Normalize(),
                    $"{retired} must normalize to a supported output.");
            }
        }

        [TestMethod]
        public void RetiredOutputPersonasHaveNoRuntimeWriter()
        {
            // ViiperVirtualDeviceType is the packet-writer selector. A retired
            // persona must not be constructible there at all.
            CollectionAssert.AreEquivalent(
                new[]
                {
                    ViiperVirtualDeviceType.Xbox360,
                    ViiperVirtualDeviceType.DualShock4,
                },
                Enum.GetValues<ViiperVirtualDeviceType>());
        }

        [TestMethod]
        public void AboutWindowPresentsPureDS4AndCreditsUpstreamOnly()
        {
            RunOnUiThread(() =>
            {
                About about = new About();

                Assert.AreEqual("About PureDS4", about.Title);

                string text = string.Join("\n", CollectVisibleText(about));
                StringAssert.Contains(text, ProductIdentity.Name);
                Assert.IsFalse(text.Contains(RetiredProductName,
                        StringComparison.OrdinalIgnoreCase),
                    "The About window still calls the product " +
                    RetiredProductName + ".");

                // The upstream project is credited by name on purpose. That is
                // lineage attribution, not this product identity.
                StringAssert.Contains(text, "hbashton/DS4Windows");
            });
        }

        [TestMethod]
        public void OwnedRepositoryUrlsPointAtThePureDS4Repository()
        {
            foreach (string url in new[]
            {
                ProductIdentity.RepositoryUrl,
                ProductIdentity.IssuesUrl,
                ProductIdentity.ReleasesApiUrl,
            })
            {
                Assert.IsFalse(url.Contains(RetiredRepository,
                        StringComparison.OrdinalIgnoreCase),
                    url + " still points at the renamed repository.");
                StringAssert.Contains(url, "pureds4");
            }
        }

        [TestMethod]
        public void ReleaseNotesStayFailClosedAndUseTheProductName()
        {
            // No PureDS4 version or publication policy has been chosen, so the
            // build must not present inherited DS4Windows release notes.
            Assert.IsFalse(UpdateAuthorityPolicy.ProductUpdatesEnabled);
            Assert.IsFalse(UpdateAuthorityPolicy.ProductReleaseNotesEnabled);
            StringAssert.Contains(
                UpdateAuthorityPolicy.ReleaseNotesDisabledMarkdown,
                ProductIdentity.Name);
            Assert.IsFalse(UpdateAuthorityPolicy.ReleaseNotesDisabledMarkdown
                .Contains(RetiredProductName, StringComparison.Ordinal));
        }

        [TestMethod]
        public void SpecialActionEditorHidesUnsupportedHardwareTriggers()
        {
            RunOnUiThread(() =>
            {
                SpecialActionEditor editor =
                    new SpecialActionEditor(0, new ProfileList());

                List<CheckBox> boxes = Descendants(editor)
                    .OfType<CheckBox>()
                    .Where(box => box.Tag is string)
                    .ToList();

                Assert.IsTrue(boxes.Count > 0,
                    "No trigger checkboxes were found to inspect.");

                foreach (CheckBox box in boxes)
                {
                    string tag = (string)box.Tag;
                    bool unsupported =
                        UnsupportedHardwareTriggerTags.Contains(tag);

                    // The rows stay in the tree so a special action imported
                    // from another product keeps its saved trigger name, but
                    // they must not be offered as something to pick.
                    Assert.AreEqual(unsupported, IsHidden(box),
                        "Trigger " + tag + " visibility does not match the " +
                        "supported-hardware contract.");
                }
            });
        }

        /// <summary>
        /// Whether a trigger row is hidden on its own account. The unload
        /// trigger list as a whole is collapsed until an action type needs it,
        /// so ancestors above the row are deliberately not consulted.
        /// </summary>
        private static bool IsHidden(DependencyObject element)
        {
            for (DependencyObject current = element; current != null;
                current = LogicalTreeHelper.GetParent(current))
            {
                if (current is UIElement uiElement &&
                    uiElement.Visibility != Visibility.Visible)
                {
                    return true;
                }

                if (current is ListViewItem)
                {
                    return false;
                }
            }

            return false;
        }

        private static IEnumerable<DependencyObject> Descendants(
            DependencyObject root)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is not DependencyObject node)
                {
                    continue;
                }

                yield return node;
                foreach (DependencyObject descendant in Descendants(node))
                {
                    yield return descendant;
                }
            }
        }

        private static IEnumerable<string> CollectVisibleText(
            DependencyObject root)
        {
            foreach (DependencyObject node in Descendants(root))
            {
                switch (node)
                {
                    case TextBlock textBlock when
                        !string.IsNullOrWhiteSpace(textBlock.Text):
                        yield return textBlock.Text;
                        break;
                    case HeaderedContentControl headered when
                        headered.Header is string header:
                        yield return header;
                        break;
                    case ContentControl content when
                        content.Content is string label:
                        yield return label;
                        break;
                }

                if (node is FrameworkElement element)
                {
                    if (element.ToolTip is string tip)
                    {
                        yield return tip;
                    }

                    string automationName =
                        AutomationProperties.GetName(element);
                    if (!string.IsNullOrWhiteSpace(automationName))
                    {
                        yield return automationName;
                    }
                }
            }
        }

        private static void RunOnUiThread(Action body)
        {
            WpfTestHost.Run(() =>
            {
                WpfTestHost.LoadApplicationThemes(replaceExisting: true);
                body();
            });
        }
    }
}
