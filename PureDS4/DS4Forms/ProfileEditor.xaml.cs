using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NonFormTimer = System.Timers.Timer;
using DS4WinWPF.DS4Forms.ViewModels;
using DS4Windows;
using System.ComponentModel;
using System.Windows.Forms;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using MessageBox = DS4WinWPF.DS4Forms.AppDialog;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;
using DS4Windows.InputDevices;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Interaction logic for ProfileEditor.xaml
    /// </summary>
    public partial class ProfileEditor : UserControl
    {
        private class HoverImageInfo
        {
            public Point point;
            public Size size;
        }

        private int deviceNum;
        private readonly int triggerPreviewDeviceIndex;
        private ProfileSettingsViewModel profileSettingsVM;
        private MappingListViewModel mappingListVM;
        private ProfileEntity currentProfile;
        private SpecialActionsListViewModel specialActionsVM;
        private double controllerCoordinateScale = 1.0;
        private double controllerCoordinateOffsetX;

        public event EventHandler Closed;
        public event EventHandler ProfileNameChanged;

        public delegate void CreatedProfileHandler(ProfileEditor sender, string profile);

        public event CreatedProfileHandler CreatedProfile;

        private Dictionary<Button, ImageBrush> hoverImages =
            new Dictionary<Button, ImageBrush>();

        private Dictionary<Button, HoverImageInfo> hoverLocations = new Dictionary<Button, HoverImageInfo>();
        private Dictionary<Button, int> hoverIndexes = new Dictionary<Button, int>();
        private Dictionary<int, Button> reverseHoverIndexes = new Dictionary<int, Button>();
        private Dictionary<Button, ImageSource> controllerHoverImages = new Dictionary<Button, ImageSource>();
        private Dictionary<Button, Geometry> vectorHoverGeometries = new Dictionary<Button, Geometry>();
        private readonly HashSet<Button> rasterHitGeometryButtons = new HashSet<Button>();

        private bool controllerReadingsTabActive = false;
        private bool sectionNavigationChanging;

        public int DeviceNum
        {
            get => deviceNum;
        }

        public string ProfileName => profileNameTxt.Text;

        private NonFormTimer inputTimer;

        private TouchButtonUserControl touchButtonUC;
        private ContentControl activeTouchButtonDisplayControl;

        public ProfileEditor(int device, int controllerContextDevice = -1)
        {
            InitializeComponent();

            deviceNum = device;
            triggerPreviewDeviceIndex = ResolveControllerContextIndex(
                device, controllerContextDevice);
            emptyColorGB.Visibility = Visibility.Collapsed;
            DS4Device physicalController = ResolveControllerContext(
                device, controllerContextDevice);
            profileSettingsVM = new ProfileSettingsViewModel(device,
                physicalController?.DeviceType,
                physicalController?.ConnectionType,
                physicalController?.HidDevice?.Attributes?.VendorId,
                physicalController?.HidDevice?.Attributes?.ProductId);
            picBoxHover.Visibility = Visibility.Hidden;
            picBoxHover2.Visibility = Visibility.Hidden;

            ConfigureControllerDiagram();

            mappingListVM = new MappingListViewModel(deviceNum,
                profileSettingsVM.ContType, physicalController?.DeviceType);
            specialActionsVM = new SpecialActionsListViewModel(device);

            touchButtonUC = new TouchButtonUserControl(device);
            TouchpadButtonControlDisplaySetup();

            RemoveHoverBtnText();
            PopulateHoverImages();
            PopulateHoverLocations();
            PopulateHoverIndexes();
            PopulateReverseHoverIndexes();
            PopulateGyroActionsTriggersMenu();

            AssignTiltAssociation();
            AssignSwipeAssociation();
            AssignStickOuterBindAssociation();
            UpdateOutputControllerHint(profileSettingsVM.TempConType);
            AssignGyroSwipeAssociation();

            inputTimer = new NonFormTimer(100);
            inputTimer.Elapsed += InputDS4;
            SetupEvents();
        }

        private void ConfigureControllerDiagram()
        {
            controllerHoverImages.Clear();
            vectorHoverGeometries.Clear();
            rasterHitGeometryButtons.Clear();
            ClearControllerButtonClips();
            HideControllerHover();

            muteConBtn.Visibility = Visibility.Collapsed;
            captureConBtn.Visibility = Visibility.Collapsed;
            fnlConBtn.Visibility = Visibility.Collapsed;
            fnrConBtn.Visibility = Visibility.Collapsed;
            blpConBtn.Visibility = Visibility.Collapsed;
            brpConBtn.Visibility = Visibility.Collapsed;
            leftTouchConBtn.Visibility = Visibility.Visible;
            multiTouchConBtn.Visibility = Visibility.Visible;
            rightTouchConBtn.Visibility = Visibility.Visible;
            topTouchConBtn.Visibility = Visibility.Visible;
            ds4LightbarColorBtn.Visibility = Visibility.Visible;

            lightbarRect.OpacityMask = null;
            lightbarRect.RadiusX = 2;
            lightbarRect.RadiusY = 2;
            ds4LightbarColorBtn.Clip = null;

            ConfigureDualShock4Diagram();
        }

        private void ConfigureDualShock4Diagram()
        {
            ConfigureControllerRaster("DualShock 4 Controller.png",
                "DualShock 4 remapping layout", 384, 247);

            SetCanvasButtonBounds(crossConBtn, 337, 153, 34, 34);
            SetCanvasButtonBounds(circleConBtn, 368, 128, 34, 34);
            SetCanvasButtonBounds(squareConBtn, 304, 128, 34, 34);
            SetCanvasButtonBounds(triangleConBtn, 337, 104, 34, 34);

            SetCanvasButtonBounds(l1ConBtn, 56, 56, 64, 34);
            SetCanvasButtonBounds(r1ConBtn, 320, 56, 64, 34);
            SetCanvasButtonBounds(l2ConBtn, 56, 22, 64, 36);
            SetCanvasButtonBounds(r2ConBtn, 320, 22, 64, 36);
            SetCanvasButtonBounds(shareConBtn, 123, 91, 19, 31);
            SetCanvasButtonBounds(optionsConBtn, 298, 91, 19, 31);
            SetCanvasButtonBounds(guideConBtn, 205, 178, 30, 28);

            // Keep the four touch gestures distinct and non-overlapping. The
            // previous bounds placed the multi-touch target underneath both
            // side targets, making most of it impossible to select.
            SetCanvasButtonBounds(topTouchConBtn, 149, 71, 144, 20);
            SetCanvasButtonBounds(leftTouchConBtn, 149, 90, 58, 65);
            SetCanvasButtonBounds(multiTouchConBtn, 206, 90, 30, 65);
            SetCanvasButtonBounds(rightTouchConBtn, 235, 90, 58, 65);

            SetCanvasButtonBounds(l3ConBtn, 121, 178, 59, 42);
            SetCanvasButtonBounds(lsuConBtn, 137, 178, 27, 14);
            SetCanvasButtonBounds(lsrConBtn, 164, 191, 16, 25);
            SetCanvasButtonBounds(lsdConBtn, 137, 207, 27, 13);
            SetCanvasButtonBounds(lslConBtn, 121, 191, 16, 25);

            SetCanvasButtonBounds(r3ConBtn, 261, 178, 59, 42);
            SetCanvasButtonBounds(rsuConBtn, 277, 178, 27, 14);
            SetCanvasButtonBounds(rsrConBtn, 304, 191, 16, 25);
            SetCanvasButtonBounds(rsdConBtn, 277, 207, 27, 13);
            SetCanvasButtonBounds(rslConBtn, 261, 191, 16, 25);

            SetCanvasButtonBounds(upConBtn, 72, 113, 32, 35);
            SetCanvasButtonBounds(rightConBtn, 94, 128, 35, 34);
            SetCanvasButtonBounds(downConBtn, 72, 150, 32, 35);
            SetCanvasButtonBounds(leftConBtn, 49, 128, 35, 34);

            SetControllerElementBounds(ds4LightbarColorBtn, 149, 53, 143, 16);

            PopulateDualShock4VectorHighlights();
            PopulateControllerHoverAtlas("DualShock4-Config_Highlights.png",
                includeMute: false, includeTouch: true,
                includeEdgeControls: false, includeCapture: false);
            PopulateControllerStickAtlas("DualShock4-Stick_Highlights.png");
            lightbarRect.OpacityMask = new ImageBrush(
                LoadResourceImage("DualShock4-Mapping-Lightbar.png"));
            ApplyControllerButtonClips();
        }

        private void ConfigureControllerRaster(string resourceName, string toolTip,
            double sourceWidth, double sourceHeight)
        {
            controllerDiagram.Source = LoadResourceImage(resourceName);
            controllerDiagram.ToolTip = null;
            controllerDiagram.Width = 440;
            controllerDiagram.Height = 220;
            controllerDiagram.Stretch = Stretch.Uniform;
            Canvas.SetLeft(controllerDiagram, 0);
            Canvas.SetTop(controllerDiagram, 0);
            controllerDiagram.Clip = null;

            double renderedScale = Math.Min(440.0 / sourceWidth,
                220.0 / sourceHeight);
            double renderedWidth = sourceWidth * renderedScale;
            controllerCoordinateScale = renderedWidth / 440.0;
            controllerCoordinateOffsetX = (440.0 - renderedWidth) / 2.0;
        }

        private static Geometry EllipseHighlight(double x, double y, double width, double height)
        {
            return new EllipseGeometry(new Rect(x, y, width, height));
        }

        private static Geometry RoundedHighlight(double x, double y, double width,
            double height, double radius = 4)
        {
            return new RectangleGeometry(new Rect(x, y, width, height), radius, radius);
        }

        private static Geometry PolygonHighlight(params Point[] points)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(points[0], true, true);
                for (int i = 1; i < points.Length; i++)
                {
                    context.LineTo(points[i], true, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private static Geometry TouchSegment(Geometry touchpadOutline,
            double x, double y, double width, double height)
        {
            var segment = new CombinedGeometry(GeometryCombineMode.Intersect,
                touchpadOutline, new RectangleGeometry(new Rect(x, y, width, height)));
            segment.Freeze();
            return segment;
        }

        private static Geometry DpadHighlight(double x, double y, double width,
            double height, int direction)
        {
            Point[] normalized = direction switch
            {
                0 => new[] { new Point(.22, 1), new Point(.08, .78), new Point(.10, .22), new Point(.28, .04), new Point(.72, .04), new Point(.90, .22), new Point(.92, .78), new Point(.78, 1) },
                1 => new[] { new Point(0, .22), new Point(.22, .08), new Point(.78, .10), new Point(.96, .28), new Point(.96, .72), new Point(.78, .90), new Point(.22, .92), new Point(0, .78) },
                2 => new[] { new Point(.22, 0), new Point(.08, .22), new Point(.10, .78), new Point(.28, .96), new Point(.72, .96), new Point(.90, .78), new Point(.92, .22), new Point(.78, 0) },
                _ => new[] { new Point(1, .22), new Point(.78, .08), new Point(.22, .10), new Point(.04, .28), new Point(.04, .72), new Point(.22, .90), new Point(.78, .92), new Point(1, .78) },
            };

            return PolygonHighlight(normalized.Select(point =>
                new Point(x + point.X * width, y + point.Y * height)).ToArray());
        }

        private static Geometry StickPressHighlight(double x, double y,
            double width, double height)
        {
            // L3/R3 are the center press surface, not the entire analog-stick
            // assembly. Keeping this inset also leaves an obvious visual lane
            // for the four directional mappings around it.
            const double inset = 0.27;
            return EllipseHighlight(x + width * inset, y + height * inset,
                width * (1.0 - inset * 2.0), height * (1.0 - inset * 2.0));
        }

        private static Geometry StickDirectionHighlight(double x, double y,
            double width, double height, int direction)
        {
            Geometry outer = EllipseHighlight(x + width * 0.05,
                y + height * 0.05, width * 0.90, height * 0.90);
            Geometry center = EllipseHighlight(x + width * 0.29,
                y + height * 0.29, width * 0.42, height * 0.42);
            Geometry ring = new CombinedGeometry(GeometryCombineMode.Exclude,
                outer, center);

            Point[] points = direction switch
            {
                0 => new[]
                {
                    new Point(x + width * 0.12, y),
                    new Point(x + width * 0.88, y),
                    new Point(x + width * 0.63, y + height * 0.52),
                    new Point(x + width * 0.37, y + height * 0.52),
                },
                1 => new[]
                {
                    new Point(x + width * 0.48, y + height * 0.37),
                    new Point(x + width, y + height * 0.12),
                    new Point(x + width, y + height * 0.88),
                    new Point(x + width * 0.48, y + height * 0.63),
                },
                2 => new[]
                {
                    new Point(x + width * 0.37, y + height * 0.48),
                    new Point(x + width * 0.63, y + height * 0.48),
                    new Point(x + width * 0.88, y + height),
                    new Point(x + width * 0.12, y + height),
                },
                _ => new[]
                {
                    new Point(x, y + height * 0.12),
                    new Point(x + width * 0.52, y + height * 0.37),
                    new Point(x + width * 0.52, y + height * 0.63),
                    new Point(x, y + height * 0.88),
                },
            };

            var result = new CombinedGeometry(GeometryCombineMode.Intersect,
                ring, PolygonHighlight(points));
            result.Freeze();
            return result;
        }

        private void AddStickHighlights(Button stick, Button up, Button right,
            Button down, Button left, double x, double y, double width,
            double height)
        {
            vectorHoverGeometries[stick] = StickPressHighlight(x, y, width, height);
            vectorHoverGeometries[up] = StickDirectionHighlight(x, y, width, height, 0);
            vectorHoverGeometries[right] = StickDirectionHighlight(x, y, width, height, 1);
            vectorHoverGeometries[down] = StickDirectionHighlight(x, y, width, height, 2);
            vectorHoverGeometries[left] = StickDirectionHighlight(x, y, width, height, 3);
        }

        private void PopulateDualShock4VectorHighlights()
        {
            vectorHoverGeometries[crossConBtn] = EllipseHighlight(337, 153, 34, 34);
            vectorHoverGeometries[circleConBtn] = EllipseHighlight(368, 128, 34, 34);
            vectorHoverGeometries[squareConBtn] = EllipseHighlight(304, 128, 34, 34);
            vectorHoverGeometries[triangleConBtn] = EllipseHighlight(337, 104, 34, 34);
            vectorHoverGeometries[l1ConBtn] = RoundedHighlight(56, 56, 64, 34, 13);
            vectorHoverGeometries[r1ConBtn] = RoundedHighlight(320, 56, 64, 34, 13);
            vectorHoverGeometries[l2ConBtn] = PolygonHighlight(
                new Point(56, 57), new Point(59, 32), new Point(71, 23),
                new Point(104, 23), new Point(117, 32), new Point(120, 57));
            vectorHoverGeometries[r2ConBtn] = PolygonHighlight(
                new Point(320, 57), new Point(323, 32), new Point(336, 23),
                new Point(369, 23), new Point(381, 32), new Point(384, 57));
            vectorHoverGeometries[shareConBtn] = RoundedHighlight(123, 91, 19, 31, 8);
            vectorHoverGeometries[optionsConBtn] = RoundedHighlight(298, 91, 19, 31, 8);
            vectorHoverGeometries[guideConBtn] = EllipseHighlight(205, 178, 30, 28);

            Geometry touchpad = PolygonHighlight(
                new Point(149, 71), new Point(292, 71),
                new Point(292, 146), new Point(284, 155),
                new Point(158, 155), new Point(149, 146));
            vectorHoverGeometries[topTouchConBtn] = TouchSegment(touchpad, 149, 71, 144, 20);
            vectorHoverGeometries[leftTouchConBtn] = TouchSegment(touchpad, 149, 90, 58, 65);
            vectorHoverGeometries[multiTouchConBtn] = TouchSegment(touchpad, 206, 90, 30, 65);
            vectorHoverGeometries[rightTouchConBtn] = TouchSegment(touchpad, 235, 90, 58, 65);
            AddStickHighlights(l3ConBtn, lsuConBtn, lsrConBtn, lsdConBtn, lslConBtn,
                121, 178, 59, 59);
            AddStickHighlights(r3ConBtn, rsuConBtn, rsrConBtn, rsdConBtn, rslConBtn,
                261, 178, 59, 59);
            vectorHoverGeometries[upConBtn] = DpadHighlight(72, 113, 32, 35, 0);
            vectorHoverGeometries[rightConBtn] = DpadHighlight(94, 128, 35, 34, 1);
            vectorHoverGeometries[downConBtn] = DpadHighlight(72, 150, 32, 35, 2);
            vectorHoverGeometries[leftConBtn] = DpadHighlight(49, 128, 35, 34, 3);
        }

        private void PopulateDualSenseHitGeometries()
        {
            vectorHoverGeometries[crossConBtn] = EllipseHighlight(334, 128, 25, 25);
            vectorHoverGeometries[circleConBtn] = EllipseHighlight(364, 101, 25, 25);
            vectorHoverGeometries[squareConBtn] = EllipseHighlight(304, 101, 25, 25);
            vectorHoverGeometries[triangleConBtn] = EllipseHighlight(334, 77, 25, 25);
            vectorHoverGeometries[l1ConBtn] = RoundedHighlight(69, 26, 62, 25, 9);
            vectorHoverGeometries[r1ConBtn] = RoundedHighlight(310, 26, 62, 25, 9);
            vectorHoverGeometries[l2ConBtn] = RoundedHighlight(73, 1, 55, 25, 10);
            vectorHoverGeometries[r2ConBtn] = RoundedHighlight(315, 1, 55, 25, 10);
            vectorHoverGeometries[shareConBtn] = RoundedHighlight(116, 55, 18, 28, 7);
            vectorHoverGeometries[optionsConBtn] = RoundedHighlight(307, 55, 18, 28, 7);
            vectorHoverGeometries[guideConBtn] = EllipseHighlight(211, 152, 20, 21);
            vectorHoverGeometries[muteConBtn] = RoundedHighlight(211, 178, 20, 12, 5);

            Geometry touchpad = PolygonHighlight(
                new Point(143, 49), new Point(301, 49),
                new Point(299, 112), new Point(294, 124),
                new Point(285, 130), new Point(157, 130),
                new Point(148, 124), new Point(143, 112));
            vectorHoverGeometries[topTouchConBtn] = TouchSegment(touchpad, 143, 49, 158, 28);
            vectorHoverGeometries[leftTouchConBtn] = TouchSegment(touchpad, 143, 77, 54, 55);
            vectorHoverGeometries[multiTouchConBtn] = TouchSegment(touchpad, 197, 77, 46, 55);
            vectorHoverGeometries[rightTouchConBtn] = TouchSegment(touchpad, 243, 77, 58, 55);

            AddStickHighlights(l3ConBtn, lsuConBtn, lsrConBtn, lsdConBtn, lslConBtn,
                134, 146, 45, 46);
            AddStickHighlights(r3ConBtn, rsuConBtn, rsrConBtn, rsdConBtn, rslConBtn,
                262, 146, 45, 46);
            vectorHoverGeometries[upConBtn] = DpadHighlight(84, 76, 23, 31, 0);
            vectorHoverGeometries[rightConBtn] = DpadHighlight(103, 96, 33, 23, 1);
            vectorHoverGeometries[downConBtn] = DpadHighlight(84, 111, 23, 31, 2);
            vectorHoverGeometries[leftConBtn] = DpadHighlight(55, 96, 33, 23, 3);
        }

        private void PopulateDualSenseEdgeVectorHighlights()
        {
            vectorHoverGeometries[crossConBtn] = EllipseHighlight(326, 119, 29, 29);
            vectorHoverGeometries[circleConBtn] = EllipseHighlight(353, 90, 30, 29);
            vectorHoverGeometries[squareConBtn] = EllipseHighlight(297, 90, 29, 29);
            vectorHoverGeometries[triangleConBtn] = EllipseHighlight(326, 61, 29, 29);
            vectorHoverGeometries[l1ConBtn] = RoundedHighlight(69, 27, 61, 30, 11);
            vectorHoverGeometries[r1ConBtn] = RoundedHighlight(310, 27, 61, 30, 11);
            vectorHoverGeometries[l2ConBtn] = PolygonHighlight(
                new Point(70, 28), new Point(72, 12), new Point(78, 6),
                new Point(105, 6), new Point(118, 10), new Point(127, 28));
            vectorHoverGeometries[r2ConBtn] = PolygonHighlight(
                new Point(313, 28), new Point(322, 10), new Point(335, 6),
                new Point(362, 6), new Point(368, 12), new Point(370, 28));
            vectorHoverGeometries[shareConBtn] = RoundedHighlight(123, 56, 15, 20, 7);
            vectorHoverGeometries[optionsConBtn] = RoundedHighlight(304, 56, 15, 20, 7);
            vectorHoverGeometries[guideConBtn] = RoundedHighlight(206, 144, 27, 27, 5);
            vectorHoverGeometries[muteConBtn] = RoundedHighlight(210, 179, 21, 9, 4);

            Geometry touchpad = PolygonHighlight(
                new Point(141, 44), new Point(300, 44),
                new Point(294, 97), new Point(285, 112),
                new Point(272, 118), new Point(167, 118),
                new Point(155, 112), new Point(146, 97));
            vectorHoverGeometries[topTouchConBtn] = TouchSegment(touchpad, 141, 44, 159, 21);
            vectorHoverGeometries[leftTouchConBtn] = TouchSegment(touchpad, 142, 64, 57, 54);
            vectorHoverGeometries[multiTouchConBtn] = TouchSegment(touchpad, 198, 64, 45, 54);
            vectorHoverGeometries[rightTouchConBtn] = TouchSegment(touchpad, 242, 64, 57, 54);
            AddStickHighlights(l3ConBtn, lsuConBtn, lsrConBtn, lsdConBtn, lslConBtn,
                132, 130, 55, 55);
            AddStickHighlights(r3ConBtn, rsuConBtn, rsrConBtn, rsdConBtn, rslConBtn,
                253, 130, 55, 55);
            vectorHoverGeometries[upConBtn] = DpadHighlight(88, 72, 25, 28, 0);
            vectorHoverGeometries[rightConBtn] = DpadHighlight(105, 92, 30, 25, 1);
            vectorHoverGeometries[downConBtn] = DpadHighlight(88, 109, 25, 28, 2);
            vectorHoverGeometries[leftConBtn] = DpadHighlight(66, 92, 30, 25, 3);
            vectorHoverGeometries[fnlConBtn] = RoundedHighlight(147, 205, 25, 15, 5);
            vectorHoverGeometries[fnrConBtn] = RoundedHighlight(269, 205, 25, 15, 5);
        }

        private void PopulateControllerHoverAtlas(string resourceName,
            bool includeMute, bool includeTouch, bool includeEdgeControls,
            bool includeCapture)
        {
            const bool exactRasterHitTest = true;
            void Assign(Button button, int frameIndex)
            {
                controllerHoverImages[button] = RasterHighlightAtlas.Frame(
                    resourceName, frameIndex);
                if (exactRasterHitTest)
                {
                    AssignControllerRasterHitGeometry(button, resourceName,
                        frameIndex);
                }
            }

            Assign(crossConBtn, 0);
            Assign(circleConBtn, 1);
            Assign(squareConBtn, 2);
            Assign(triangleConBtn, 3);
            Assign(l1ConBtn, 4);
            Assign(r1ConBtn, 5);
            Assign(l2ConBtn, 6);
            Assign(r2ConBtn, 7);
            Assign(shareConBtn, 8);
            Assign(optionsConBtn, 9);
            Assign(guideConBtn, 10);
            if (includeMute)
            {
                Assign(muteConBtn, 11);
            }

            // Stick presses and stick directions use controller-space vector
            // masks. Atlas frames 12 and 13 historically painted the entire
            // stick for every one of those mappings, which made Up/Right/Down/
            // Left indistinguishable and made L3/R3 much too large.

            Assign(upConBtn, 14);
            Assign(rightConBtn, 15);
            Assign(downConBtn, 16);
            Assign(leftConBtn, 17);

            if (includeTouch)
            {
                Assign(leftTouchConBtn, 18);
                Assign(multiTouchConBtn, 19);
                Assign(rightTouchConBtn, 20);
                Assign(topTouchConBtn, 21);
            }

            if (includeEdgeControls)
            {
                Assign(fnlConBtn, 22);
                Assign(fnrConBtn, 23);
                Assign(blpConBtn, 24);
                Assign(brpConBtn, 25);
            }

            if (includeCapture)
            {
                Assign(captureConBtn, 26);
            }
        }

        private void PopulateControllerStickAtlas(string resourceName)
        {
            Button[] buttons =
            {
                l3ConBtn, lsuConBtn, lsrConBtn, lsdConBtn, lslConBtn,
                r3ConBtn, rsuConBtn, rsrConBtn, rsdConBtn, rslConBtn,
            };
            for (int frameIndex = 0; frameIndex < buttons.Length; frameIndex++)
            {
                Button button = buttons[frameIndex];
                controllerHoverImages[button] = RasterHighlightAtlas.Frame(
                    resourceName, frameIndex);
                AssignControllerRasterHitGeometry(button, resourceName,
                    frameIndex, frameIndex == 0 || frameIndex == 5,
                    frameIndex is > 0 and < 5 ? 0 :
                    frameIndex is > 5 and < 10 ? 5 : -1);
            }
        }

        private void AssignControllerRasterHitGeometry(Button button,
            string resourceName, int frameIndex, bool centerPress = false,
            int directionSurfaceFrame = -1)
        {
            Geometry mask = RasterHighlightAtlas.Mask(resourceName, frameIndex);
            if (centerPress)
            {
                mask = RasterHighlightAtlas.CenterPressMask(mask);
            }
            else if (directionSurfaceFrame >= 0)
            {
                mask = RasterHighlightAtlas.DirectionalHitMask(mask,
                    RasterHighlightAtlas.Mask(resourceName,
                        directionSurfaceFrame));
            }
            Rect bounds = mask.Bounds;
            if (bounds.IsEmpty)
            {
                return;
            }

            vectorHoverGeometries[button] = mask;
            rasterHitGeometryButtons.Add(button);
            Canvas.SetLeft(button, bounds.Left);
            Canvas.SetTop(button, bounds.Top);
            button.Width = bounds.Width;
            button.Height = bounds.Height;
        }

        private static DS4Device ResolveControllerContext(int profileDevice,
            int preferredControllerDevice)
        {
            int index = ResolveControllerContextIndex(profileDevice,
                preferredControllerDevice);
            return index >= 0 ? App.rootHub.DS4Controllers[index] : null;
        }

        private static int ResolveControllerContextIndex(int profileDevice,
            int preferredControllerDevice)
        {
            if (App.rootHub == null)
            {
                return -1;
            }

            if (preferredControllerDevice >= 0 &&
                preferredControllerDevice < ControlService.CURRENT_DS4_CONTROLLER_LIMIT &&
                App.rootHub.DS4Controllers[preferredControllerDevice] != null)
            {
                return preferredControllerDevice;
            }

            if (profileDevice >= 0 &&
                profileDevice < ControlService.CURRENT_DS4_CONTROLLER_LIMIT &&
                App.rootHub.DS4Controllers[profileDevice] != null)
            {
                return profileDevice;
            }

            // Offline/test-slot profile editing has no physical slot of its own.
            // Use the first connected controller as a deterministic fallback;
            // MainWindow normally supplies the explicitly selected controller.
            for (int i = 0; i < ControlService.CURRENT_DS4_CONTROLLER_LIMIT; i++)
            {
                if (App.rootHub.DS4Controllers[i] != null)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SetCanvasButtonBounds(Button button, double left, double top,
            double width, double height)
        {
            SetControllerElementBounds(button, left, top, width, height);
        }

        private void SetControllerElementBounds(FrameworkElement element,
            double left, double top, double width, double height)
        {
            Canvas.SetLeft(element, controllerCoordinateOffsetX +
                left * controllerCoordinateScale);
            Canvas.SetTop(element, top * controllerCoordinateScale);
            element.Width = width * controllerCoordinateScale;
            element.Height = height * controllerCoordinateScale;
        }

        private IEnumerable<Button> ControllerDiagramButtons()
        {
            yield return crossConBtn;
            yield return circleConBtn;
            yield return squareConBtn;
            yield return triangleConBtn;
            yield return l1ConBtn;
            yield return r1ConBtn;
            yield return l2ConBtn;
            yield return r2ConBtn;
            yield return shareConBtn;
            yield return optionsConBtn;
            yield return guideConBtn;
            yield return captureConBtn;
            yield return muteConBtn;
            yield return leftTouchConBtn;
            yield return multiTouchConBtn;
            yield return rightTouchConBtn;
            yield return topTouchConBtn;
            yield return l3ConBtn;
            yield return lsuConBtn;
            yield return lsrConBtn;
            yield return lsdConBtn;
            yield return lslConBtn;
            yield return r3ConBtn;
            yield return rsuConBtn;
            yield return rsrConBtn;
            yield return rsdConBtn;
            yield return rslConBtn;
            yield return upConBtn;
            yield return rightConBtn;
            yield return downConBtn;
            yield return leftConBtn;
            yield return fnlConBtn;
            yield return fnrConBtn;
            yield return blpConBtn;
            yield return brpConBtn;
        }

        private void ClearControllerButtonClips()
        {
            foreach (Button button in ControllerDiagramButtons())
            {
                button.Clip = null;
            }
        }

        private Geometry TransformControllerGeometry(Geometry geometry)
        {
            Geometry transformed = geometry.Clone();
            var transforms = new TransformGroup();
            if (transformed.Transform != null &&
                transformed.Transform != Transform.Identity)
            {
                transforms.Children.Add(transformed.Transform);
            }

            transforms.Children.Add(new MatrixTransform(
                controllerCoordinateScale, 0, 0,
                controllerCoordinateScale,
                controllerCoordinateOffsetX, 0));
            transformed.Transform = transforms;
            transformed.Freeze();
            return transformed;
        }

        private void ApplyControllerButtonClips()
        {
            foreach (KeyValuePair<Button, Geometry> entry in vectorHoverGeometries)
            {
                double left = Canvas.GetLeft(entry.Key);
                double top = Canvas.GetTop(entry.Key);
                if (double.IsNaN(left) || double.IsNaN(top))
                {
                    continue;
                }

                // Use the exact same controller-space mask for hit testing and
                // painting. In particular, stick directions are ring sectors;
                // a rounded rectangle around that sector made hover activate
                // while the pointer was visibly outside the highlighted rim.
                Geometry hitGeometry = rasterHitGeometryButtons.Contains(entry.Key)
                    ? entry.Value
                    : TransformControllerGeometry(entry.Value);

                Geometry localGeometry = hitGeometry.Clone();
                Transform existingTransform = localGeometry.Transform;
                var localTransform = new TransformGroup();
                if (existingTransform != null &&
                    existingTransform != Transform.Identity)
                {
                    localTransform.Children.Add(existingTransform);
                }
                localTransform.Children.Add(new TranslateTransform(-left, -top));
                localGeometry.Transform = localTransform;
                localGeometry.Freeze();
                entry.Key.Clip = localGeometry;
            }
        }

        private static ImageSource LoadResourceImage(string fileName)
        {
            ImageSourceConverter sourceConverter = new ImageSourceConverter();
            return sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/{fileName}") as ImageSource;
        }

        private void PopulateGyroActionsTriggersMenu()
        {
            profileSettingsVM.CreateGyroTriggerMenuItems(gyroControlsTrigBtn.ContextMenu,
                GyroControlsMenuItem_Click);

            profileSettingsVM.CreateGyroTriggerMenuItems(gyroMouseTrigBtn.ContextMenu,
                GyroMouseTrigMenuItem_Click);

            profileSettingsVM.CreateGyroTriggerMenuItems(gyroMouseStickTrigBtn.ContextMenu,
                GyroMouseStickTrigMenuItem_Click);

            profileSettingsVM.CreateGyroTriggerMenuItems(gyroSwipeTrigBtn.ContextMenu,
                GyroSwipeTrigMenuItem_Click);
        }

        private void SetupEvents()
        {
            gyroOutModeCombo.SelectionChanged += GyroOutModeCombo_SelectionChanged;
            outConTypeCombo.SelectionChanged += OutConTypeCombo_SelectionChanged;
            mappingListBox.SelectionChanged += MappingListBox_SelectionChanged;
            Closed += ProfileEditor_Closed;

            profileSettingsVM.LSDeadZoneChanged += UpdateReadingsLsDeadZone;
            profileSettingsVM.RSDeadZoneChanged += UpdateReadingsRsDeadZone;
            profileSettingsVM.L2DeadZoneChanged += UpdateReadingsL2DeadZone;
            profileSettingsVM.R2DeadZoneChanged += UpdateReadingsR2DeadZone;
            profileSettingsVM.SXDeadZoneChanged += UpdateReadingsSXDeadZone;
            profileSettingsVM.SZDeadZoneChanged += UpdateReadingsSZDeadZone;
            profileSettingsVM.TouchpadOutputIndexChanged += TouchpadOutputDisplayChange;

            profileSettingsVM.LeftStickDriftXAxisChanged += UpdateReadingsLSDrift;
            profileSettingsVM.LeftStickDriftYAxisChanged += UpdateReadingsLSDrift;
            profileSettingsVM.RightStickDriftXAxisChanged += UpdateReadingsRSDrift;
            profileSettingsVM.RightStickDriftYAxisChanged += UpdateReadingsRSDrift;
        }

        private void UnregisterEvents()
        {
            gyroOutModeCombo.SelectionChanged -= GyroOutModeCombo_SelectionChanged;
            outConTypeCombo.SelectionChanged -= OutConTypeCombo_SelectionChanged;
            mappingListBox.SelectionChanged -= MappingListBox_SelectionChanged;
            Closed -= ProfileEditor_Closed;

            profileSettingsVM.LSDeadZoneChanged -= UpdateReadingsLsDeadZone;
            profileSettingsVM.RSDeadZoneChanged -= UpdateReadingsRsDeadZone;
            profileSettingsVM.L2DeadZoneChanged -= UpdateReadingsL2DeadZone;
            profileSettingsVM.R2DeadZoneChanged -= UpdateReadingsR2DeadZone;
            profileSettingsVM.SXDeadZoneChanged -= UpdateReadingsSXDeadZone;
            profileSettingsVM.SZDeadZoneChanged -= UpdateReadingsSZDeadZone;
            profileSettingsVM.TouchpadOutputIndexChanged -= TouchpadOutputDisplayChange;

            axialLSStickControl.AxialVM.DeadZoneXChanged -= UpdateReadingsLsDeadZoneX;
            axialLSStickControl.AxialVM.DeadZoneYChanged -= UpdateReadingsLsDeadZoneY;
            axialRSStickControl.AxialVM.DeadZoneXChanged -= UpdateReadingsRsDeadZoneX;
            axialRSStickControl.AxialVM.DeadZoneYChanged -= UpdateReadingsRsDeadZoneY;

            profileSettingsVM.LeftStickDriftXAxisChanged -= UpdateReadingsLSDrift;
            profileSettingsVM.LeftStickDriftYAxisChanged -= UpdateReadingsLSDrift;
            profileSettingsVM.RightStickDriftXAxisChanged -= UpdateReadingsRSDrift;
            profileSettingsVM.RightStickDriftYAxisChanged -= UpdateReadingsRSDrift;

            inputTimer.Stop();
            inputTimer.Elapsed -= InputDS4;
            inputTimer = null;

            StopEditorBindings();
        }

        /// <summary>
        /// Place touchpad button mode options UserControl in active Touchpad TabItem.
        /// Applicable TabItem control needs to contain a ContentControl
        /// </summary>
        private void TouchpadButtonControlDisplaySetup()
        {
            ResetTouchContentControls();

            switch (profileSettingsVM.TouchpadOutputIndex)
            {
                case 1:
                    touchContentControl2.Content = touchButtonUC;
                    activeTouchButtonDisplayControl = touchContentControl2;
                    break;
                case 2:
                    touchContentControl4.Content = touchButtonUC;
                    activeTouchButtonDisplayControl = touchContentControl4;
                    break;
                case 3:
                    touchContentControl3.Content = touchButtonUC;
                    activeTouchButtonDisplayControl = touchContentControl3;
                    break;
                case 4:
                    break;

                case 0:
                default:
                    touchContentControl1.Content = touchButtonUC;
                    activeTouchButtonDisplayControl = touchContentControl1;
                    break;
            }
        }

        private void ResetTouchContentControls()
        {
            if (activeTouchButtonDisplayControl != null)
            {
                activeTouchButtonDisplayControl.Content = null;
                activeTouchButtonDisplayControl = null;
            }
        }

        private void TouchpadOutputDisplayChange(object sender, EventArgs e)
        {
            TouchpadButtonControlDisplaySetup();
        }

        private void UpdateReadingsSZDeadZone(object sender, EventArgs e)
        {
            conReadingsUserCon.SixAxisZDead = profileSettingsVM.SZDeadZone;
        }

        private void UpdateReadingsSXDeadZone(object sender, EventArgs e)
        {
            conReadingsUserCon.SixAxisXDead = profileSettingsVM.SXDeadZone;
        }

        private void UpdateReadingsR2DeadZone(object sender, EventArgs e)
        {
            conReadingsUserCon.R2Dead = profileSettingsVM.R2DeadZone;
        }

        private void UpdateReadingsL2DeadZone(object sender, EventArgs e)
        {
            conReadingsUserCon.L2Dead = profileSettingsVM.L2DeadZone;
        }

        private void UpdateReadingsLsDeadZone(object sender, EventArgs e)
        {
            conReadingsUserCon.LsDeadX = profileSettingsVM.LSDeadZone;
            conReadingsUserCon.LsDeadY = profileSettingsVM.LSDeadZone;
        }

        private void UpdateReadingsLsDeadZoneX(object sender, EventArgs e)
        {
            conReadingsUserCon.LsDeadX = axialLSStickControl.AxialVM.DeadZoneX;
        }

        private void UpdateReadingsLsDeadZoneY(object sender, EventArgs e)
        {
            conReadingsUserCon.LsDeadY = axialLSStickControl.AxialVM.DeadZoneY;
        }

        private void UpdateReadingsRsDeadZone(object sender, EventArgs e)
        {
            conReadingsUserCon.RsDeadX = profileSettingsVM.RSDeadZone;
            conReadingsUserCon.RsDeadY = profileSettingsVM.RSDeadZone;
        }

        private void UpdateReadingsRsDeadZoneX(object sender, EventArgs e)
        {
            conReadingsUserCon.RsDeadX = axialRSStickControl.AxialVM.DeadZoneX;
        }

        private void UpdateReadingsRsDeadZoneY(object sender, EventArgs e)
        {
            conReadingsUserCon.RsDeadY = axialRSStickControl.AxialVM.DeadZoneY;
        }

        private void UpdateReadingsLSDrift(object sender, EventArgs e)
        {
            conReadingsUserCon.LsDriftX = profileSettingsVM.LeftStickDriftXAxis;
            conReadingsUserCon.LsDriftY = profileSettingsVM.LeftStickDriftYAxis;
        }

        private void UpdateReadingsRSDrift(object sender, EventArgs e)
        {
            conReadingsUserCon.RsDriftX = profileSettingsVM.RightStickDriftXAxis;
            conReadingsUserCon.RsDriftY = profileSettingsVM.RightStickDriftYAxis;
        }

        private void AssignTiltAssociation()
        {
            gyroZNLb.DataContext = mappingListVM.ControlMap[DS4Windows.DS4Controls.GyroZNeg];
            gyroZPLb.DataContext = mappingListVM.ControlMap[DS4Windows.DS4Controls.GyroZPos];
            gyroXNLb.DataContext = mappingListVM.ControlMap[DS4Windows.DS4Controls.GyroXNeg];
            gyroXLb.DataContext = mappingListVM.ControlMap[DS4Windows.DS4Controls.GyroXPos];
        }

        private void AssignSwipeAssociation()
        {
            swipeUpLb.DataContext = mappingListVM.ControlMap[DS4Windows.DS4Controls.SwipeUp];
            swipeDownLb.DataContext = mappingListVM.ControlMap[DS4Windows.DS4Controls.SwipeDown];
            swipeLeftLb.DataContext = mappingListVM.ControlMap[DS4Windows.DS4Controls.SwipeLeft];
            swipeRightLb.DataContext = mappingListVM.ControlMap[DS4Windows.DS4Controls.SwipeRight];
        }

        private void AssignStickOuterBindAssociation()
        {
            lsOuterBindLb.DataContext = mappingListVM.ControlMap[DS4Controls.LSOuter];
            rsOuterBindLb.DataContext = mappingListVM.ControlMap[DS4Controls.RSOuter];
        }

        private void AssignGyroSwipeAssociation()
        {
            gyroSwipeLeftLb.DataContext = mappingListVM.ControlMap[DS4Controls.GyroSwipeLeft];
            gyroSwipeRightLb.DataContext = mappingListVM.ControlMap[DS4Controls.GyroSwipeRight];
            gyroSwipeUpLb.DataContext = mappingListVM.ControlMap[DS4Controls.GyroSwipeUp];
            gyroSwipeDownLb.DataContext = mappingListVM.ControlMap[DS4Controls.GyroSwipeDown];
        }

        private void MappingListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mappingListVM.SelectedIndex >= 0)
            {
                if (reverseHoverIndexes.TryGetValue(mappingListVM.SelectedIndex, out Button tempBtn))
                {
                    InputControlHighlight(tempBtn);
                }
                else
                {
                    HideControllerHover();
                }
            }

        }

        private void PopulateReverseHoverIndexes()
        {
            foreach (KeyValuePair<Button, int> pair in hoverIndexes)
            {
                reverseHoverIndexes.Add(pair.Value, pair.Key);
            }
        }

        private void PopulateHoverIndexes()
        {
            // Every entry is resolved through ControlIndexMap rather than a
            // hard-coded position. The mapping list omits controls that exist
            // only on hardware PureDS4 does not support, so a literal index
            // would silently point a diagram button at the wrong row.
            void MapButton(Button button, DS4Controls control)
            {
                if (mappingListVM.ControlIndexMap.TryGetValue(control,
                    out int index))
                {
                    hoverIndexes[button] = index;
                }
            }

            MapButton(crossConBtn, DS4Controls.Cross);
            MapButton(circleConBtn, DS4Controls.Circle);
            MapButton(squareConBtn, DS4Controls.Square);
            MapButton(triangleConBtn, DS4Controls.Triangle);
            MapButton(optionsConBtn, DS4Controls.Options);
            MapButton(shareConBtn, DS4Controls.Share);
            MapButton(upConBtn, DS4Controls.DpadUp);
            MapButton(downConBtn, DS4Controls.DpadDown);
            MapButton(leftConBtn, DS4Controls.DpadLeft);
            MapButton(rightConBtn, DS4Controls.DpadRight);
            MapButton(guideConBtn, DS4Controls.PS);
            MapButton(muteConBtn, DS4Controls.Mute);
            MapButton(l1ConBtn, DS4Controls.L1);
            MapButton(r1ConBtn, DS4Controls.R1);
            MapButton(l2ConBtn, DS4Controls.L2);
            MapButton(r2ConBtn, DS4Controls.R2);
            MapButton(l3ConBtn, DS4Controls.L3);
            MapButton(r3ConBtn, DS4Controls.R3);
            MapButton(captureConBtn, DS4Controls.Capture);

            MapButton(leftTouchConBtn, DS4Controls.TouchLeft);
            MapButton(rightTouchConBtn, DS4Controls.TouchRight);
            MapButton(multiTouchConBtn, DS4Controls.TouchMulti);
            MapButton(topTouchConBtn, DS4Controls.TouchUpper);

            MapButton(lsuConBtn, DS4Controls.LYNeg);
            MapButton(lsdConBtn, DS4Controls.LYPos);
            MapButton(lslConBtn, DS4Controls.LXNeg);
            MapButton(lsrConBtn, DS4Controls.LXPos);

            MapButton(rsuConBtn, DS4Controls.RYNeg);
            MapButton(rsdConBtn, DS4Controls.RYPos);
            MapButton(rslConBtn, DS4Controls.RXNeg);
            MapButton(rsrConBtn, DS4Controls.RXPos);

            MapButton(gyroZNBtn, DS4Controls.GyroZNeg);
            MapButton(gyroZPBtn, DS4Controls.GyroZPos);
            MapButton(gyroXNBtn, DS4Controls.GyroXNeg);
            MapButton(gyroXPBtn, DS4Controls.GyroXPos);

            MapButton(swipeUpBtn, DS4Controls.SwipeUp);
            MapButton(swipeDownBtn, DS4Controls.SwipeDown);
            MapButton(swipeLeftBtn, DS4Controls.SwipeLeft);
            MapButton(swipeRightBtn, DS4Controls.SwipeRight);

            MapButton(fnlConBtn, DS4Controls.FnL);
            MapButton(fnrConBtn, DS4Controls.FnR);
            MapButton(blpConBtn, DS4Controls.BLP);
            MapButton(brpConBtn, DS4Controls.BRP);
        }

        private void PopulateHoverLocations()
        {
            hoverLocations[crossConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(crossConBtn), Canvas.GetTop(crossConBtn)),
                size = new Size(crossConBtn.Width, crossConBtn.Height)
            };
            hoverLocations[circleConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(circleConBtn), Canvas.GetTop(circleConBtn)),
                size = new Size(circleConBtn.Width, circleConBtn.Height)
            };
            hoverLocations[squareConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(squareConBtn), Canvas.GetTop(squareConBtn)),
                size = new Size(squareConBtn.Width, squareConBtn.Height)
            };
            hoverLocations[triangleConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(triangleConBtn), Canvas.GetTop(triangleConBtn)),
                size = new Size(triangleConBtn.Width, triangleConBtn.Height)
            };
            hoverLocations[l1ConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(l1ConBtn), Canvas.GetTop(l1ConBtn)),
                size = new Size(l1ConBtn.Width, l1ConBtn.Height)
            };
            hoverLocations[r1ConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(r1ConBtn), Canvas.GetTop(r1ConBtn)),
                size = new Size(r1ConBtn.Width, r1ConBtn.Height)
            };
            hoverLocations[l2ConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(l2ConBtn), Canvas.GetTop(l2ConBtn)),
                size = new Size(l2ConBtn.Width, l2ConBtn.Height)
            };
            hoverLocations[r2ConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(r2ConBtn), Canvas.GetTop(r2ConBtn)),
                size = new Size(r2ConBtn.Width, r2ConBtn.Height)
            };
            hoverLocations[shareConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(shareConBtn), Canvas.GetTop(shareConBtn)),
                size = new Size(shareConBtn.Width, shareConBtn.Height)
            };
            hoverLocations[optionsConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(optionsConBtn), Canvas.GetTop(optionsConBtn)),
                size = new Size(optionsConBtn.Width, optionsConBtn.Height)
            };
            hoverLocations[guideConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(guideConBtn), Canvas.GetTop(guideConBtn)),
                size = new Size(guideConBtn.Width, guideConBtn.Height)
            };
            hoverLocations[muteConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(muteConBtn), Canvas.GetTop(muteConBtn)),
                size = new Size(muteConBtn.Width, muteConBtn.Height)
            };

            hoverLocations[leftTouchConBtn] = new HoverImageInfo()
                { point = new Point(144, 44), size = new Size(140, 98) };
            hoverLocations[multiTouchConBtn] = new HoverImageInfo()
                { point = new Point(143, 42), size = new Size(158, 100) };
            hoverLocations[rightTouchConBtn] = new HoverImageInfo()
                { point = new Point(156, 47), size = new Size(146, 94) };
            hoverLocations[topTouchConBtn] = new HoverImageInfo()
                { point = new Point(155, 6), size = new Size(153, 114) };
        


            hoverLocations[l3ConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(l3ConBtn), Canvas.GetTop(l3ConBtn)),
                size = new Size(l3ConBtn.Width, l3ConBtn.Height)
            };
            hoverLocations[lsuConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(l3ConBtn), Canvas.GetTop(l3ConBtn)),
                size = new Size(l3ConBtn.Width, l3ConBtn.Height)
            };
            hoverLocations[lsrConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(l3ConBtn), Canvas.GetTop(l3ConBtn)),
                size = new Size(l3ConBtn.Width, l3ConBtn.Height)
            };
            hoverLocations[lsdConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(l3ConBtn), Canvas.GetTop(l3ConBtn)),
                size = new Size(l3ConBtn.Width, l3ConBtn.Height)
            };
            hoverLocations[lslConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(l3ConBtn), Canvas.GetTop(l3ConBtn)),
                size = new Size(l3ConBtn.Width, l3ConBtn.Height)
            };

            hoverLocations[r3ConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(r3ConBtn), Canvas.GetTop(r3ConBtn)),
                size = new Size(r3ConBtn.Width, r3ConBtn.Height)
            };
            hoverLocations[rsuConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(r3ConBtn), Canvas.GetTop(r3ConBtn)),
                size = new Size(r3ConBtn.Width, r3ConBtn.Height)
            };
            hoverLocations[rsrConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(r3ConBtn), Canvas.GetTop(r3ConBtn)),
                size = new Size(r3ConBtn.Width, r3ConBtn.Height)
            };
            hoverLocations[rsdConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(r3ConBtn), Canvas.GetTop(r3ConBtn)),
                size = new Size(r3ConBtn.Width, r3ConBtn.Height)
            };
            hoverLocations[rslConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(r3ConBtn), Canvas.GetTop(r3ConBtn)),
                size = new Size(r3ConBtn.Width, r3ConBtn.Height)
            };

            hoverLocations[upConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(upConBtn), Canvas.GetTop(upConBtn)),
                size = new Size(upConBtn.Width, upConBtn.Height)
            };
            hoverLocations[rightConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(rightConBtn), Canvas.GetTop(rightConBtn)),
                size = new Size(rightConBtn.Width, rightConBtn.Height)
            };
            hoverLocations[downConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(downConBtn), Canvas.GetTop(downConBtn)),
                size = new Size(downConBtn.Width, downConBtn.Height)
            };
            hoverLocations[leftConBtn] = new HoverImageInfo()
            {
                point = new Point(Canvas.GetLeft(leftConBtn), Canvas.GetTop(leftConBtn)),
                size = new Size(leftConBtn.Width, leftConBtn.Height)
            };
        }

        private void RemoveHoverBtnText()
        {
            crossConBtn.Content = "";
            circleConBtn.Content = "";
            squareConBtn.Content = "";
            triangleConBtn.Content = "";
            l1ConBtn.Content = "";
            r1ConBtn.Content = "";
            l2ConBtn.Content = "";
            r2ConBtn.Content = "";
            shareConBtn.Content = "";
            optionsConBtn.Content = "";
            guideConBtn.Content = "";
            captureConBtn.Content = "";
            muteConBtn.Content = "";
            leftTouchConBtn.Content = "";
            multiTouchConBtn.Content = "";
            rightTouchConBtn.Content = "";
            topTouchConBtn.Content = "";

            l3ConBtn.Content = "";
            lsuConBtn.Content = "";
            lsrConBtn.Content = "";
            lsdConBtn.Content = "";
            lslConBtn.Content = "";

            r3ConBtn.Content = "";
            rsuConBtn.Content = "";
            rsrConBtn.Content = "";
            rsdConBtn.Content = "";
            rslConBtn.Content = "";

            upConBtn.Content = "";
            rightConBtn.Content = "";
            downConBtn.Content = "";
            leftConBtn.Content = "";

            fnlConBtn.Content = "";
            fnrConBtn.Content = "";
            blpConBtn.Content = "";
            brpConBtn.Content = "";
        }

        private void PopulateHoverImages()
        {
            // All current diagrams use either the exact full-canvas DualSense
            // atlas or controller-specific vector masks. Avoid decoding the old
            // per-button DS4 bitmap set every time the editor opens.
            return;

#pragma warning disable CS0162

            ImageSourceConverter sourceConverter = new ImageSourceConverter();

            ImageSource temp =
                sourceConverter.ConvertFromString(
                    $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Cross.png") as ImageSource;
            ImageBrush crossHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Circle.png") as ImageSource;
            ImageBrush circleHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Square.png") as ImageSource;
            ImageBrush squareHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Triangle.png") as ImageSource;
            ImageBrush triangleHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_L1.png") as ImageSource;
            ImageBrush l1Hover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_R1.png") as ImageSource;
            ImageBrush r1Hover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_L2.png") as ImageSource;
            ImageBrush l2Hover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_R2.png") as ImageSource;
            ImageBrush r2Hover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Share.png") as ImageSource;
            ImageBrush shareHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_options.png") as ImageSource;
            ImageBrush optionsHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_PS.png") as ImageSource;
            ImageBrush guideHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/" +
                "DS4-Config_PS.png") as ImageSource;
            ImageBrush muteHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/" +
                "DS4-Config_TouchLeft.png") as ImageSource;
            ImageBrush leftTouchHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/" +
                "DS4-Config_TouchMulti.png") as ImageSource;
            ImageBrush multiTouchTouchHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/" +
                "DS4-Config_TouchRight.png") as ImageSource;
            ImageBrush rightTouchHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/" +
                "DS4-Config_TouchUpper.png") as ImageSource;
            ImageBrush topTouchHover = new ImageBrush(temp);


            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_LS.png") as ImageSource;
            ImageBrush l3Hover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_LS.png") as ImageSource;
            ImageBrush lsuHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_LS.png") as ImageSource;
            ImageBrush lsrHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_LS.png") as ImageSource;
            ImageBrush lsdHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_LS.png") as ImageSource;
            ImageBrush lslHover = new ImageBrush(temp);


            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_RS.png") as ImageSource;
            ImageBrush r3Hover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_RS.png") as ImageSource;
            ImageBrush rsuHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_RS.png") as ImageSource;
            ImageBrush rsrHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_RS.png") as ImageSource;
            ImageBrush rsdHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_RS.png") as ImageSource;
            ImageBrush rslHover = new ImageBrush(temp);


            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Up.png") as ImageSource;
            ImageBrush upHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Right.png") as ImageSource;
            ImageBrush rightHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Down.png") as ImageSource;
            ImageBrush downHover = new ImageBrush(temp);

            temp = sourceConverter.ConvertFromString(
                $"{Global.ASSEMBLY_RESOURCE_PREFIX}component/Resources/DS4-Config_Left.png") as ImageSource;
            ImageBrush leftHover = new ImageBrush(temp);

            hoverImages[crossConBtn] = crossHover;
            hoverImages[circleConBtn] = circleHover;
            hoverImages[squareConBtn] = squareHover;
            hoverImages[triangleConBtn] = triangleHover;
            hoverImages[l1ConBtn] = l1Hover;
            hoverImages[r1ConBtn] = r1Hover;
            hoverImages[l2ConBtn] = l2Hover;
            hoverImages[r2ConBtn] = r2Hover;
            hoverImages[shareConBtn] = shareHover;
            hoverImages[optionsConBtn] = optionsHover;
            hoverImages[guideConBtn] = guideHover;
            hoverImages[muteConBtn] = muteHover;

            hoverImages[leftTouchConBtn] = leftTouchHover;
            hoverImages[multiTouchConBtn] = multiTouchTouchHover;
            hoverImages[rightTouchConBtn] = rightTouchHover;
            hoverImages[topTouchConBtn] = topTouchHover;
            hoverImages[l3ConBtn] = l3Hover;
            hoverImages[lsuConBtn] = lsuHover;
            hoverImages[lsrConBtn] = lsrHover;
            hoverImages[lsdConBtn] = lsdHover;
            hoverImages[lslConBtn] = lslHover;
            hoverImages[r3ConBtn] = r3Hover;
            hoverImages[rsuConBtn] = rsuHover;
            hoverImages[rsrConBtn] = rsrHover;
            hoverImages[rsdConBtn] = rsdHover;
            hoverImages[rslConBtn] = rslHover;

            hoverImages[upConBtn] = upHover;
            hoverImages[rightConBtn] = rightHover;
            hoverImages[downConBtn] = downHover;
            hoverImages[leftConBtn] = leftHover;

            hoverImages[fnlConBtn] = guideHover;
            hoverImages[fnrConBtn] = guideHover;
            hoverImages[blpConBtn] = guideHover;
            hoverImages[brpConBtn] = guideHover;
#pragma warning restore CS0162
        }

        public void SelectWorkspaceSection(int sectionIndex)
        {
            // The navigation list declares SelectedIndex in XAML, so it raises
            // SelectionChanged while InitializeComponent is still building the
            // tree. The workspace tab controls are declared after it and do not
            // exist yet at that point.
            if (sectionNavigationList == null || sidebarTabControl == null ||
                profileSettingsTabCon == null)
            {
                return;
            }

            int nextSection = Math.Clamp(sectionIndex, 0, 9);
            if (sectionNavigationList.SelectedIndex != nextSection)
            {
                sectionNavigationChanging = true;
                sectionNavigationList.SelectedIndex = nextSection;
                sectionNavigationChanging = false;
            }

            if (nextSection <= 2)
            {
                profileSettingsTabCon.Visibility = Visibility.Collapsed;
                sidebarTabControl.Visibility = Visibility.Visible;
                sidebarTabControl.SelectedIndex = nextSection;
            }
            else
            {
                // Leaving controller readings must also stop its live polling timer.
                if (sidebarTabControl.SelectedIndex == 2)
                {
                    sidebarTabControl.SelectedIndex = 0;
                }

                sidebarTabControl.Visibility = Visibility.Collapsed;
                profileSettingsTabCon.Visibility = Visibility.Visible;
                profileSettingsTabCon.SelectedIndex = nextSection - 3;
            }
        }

        private void SectionNavigationList_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsInitialized || sectionNavigationChanging ||
                sectionNavigationList == null ||
                sectionNavigationList.SelectedIndex < 0)
            {
                return;
            }

            SelectWorkspaceSection(sectionNavigationList.SelectedIndex);
        }

        public void CancelEdit()
        {
            CancelBtn_Click(this, new RoutedEventArgs());
        }

        public void DeactivateLiveReadings()
        {
            if (sidebarTabControl.SelectedIndex == 2)
            {
                sidebarTabControl.SelectedIndex = 0;
            }
        }

        private void ProfileNameTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            ProfileNameChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Reload(int device, ProfileEntity profile = null, bool profileAlreadyLoaded = false)
        {
            profileSettingsTabCon.DataContext = null;
            mappingListBox.DataContext = null;
            specialActionsTab.DataContext = null;
            lightbarRect.DataContext = null;

            deviceNum = device;
            if (profile != null)
            {
                currentProfile = profile;
                if (device == Global.TEST_PROFILE_INDEX && !profileAlreadyLoaded)
                {
                    Global.ProfilePath[Global.TEST_PROFILE_INDEX] = profile.Name;
                }

                if (!profileAlreadyLoaded)
                {
                    Global.LoadProfile(device, false, App.rootHub, false);
                }
                profileNameTxt.Text = profile.Name;
                profileNameTxt.IsEnabled = false;
                applyBtn.IsEnabled = true;
            }
            else
            {
                currentProfile = null;
                PresetOptionWindow presetWin = new PresetOptionWindow();
                presetWin.SetupData(deviceNum);
                presetWin.ShowDialog();
                if (presetWin.Result == MessageBoxResult.Cancel)
                {
                    Global.LoadBlankDevProfile(device, false, App.rootHub, false);
                }
            }


            ColorByBatteryPerCheck();

            if (device < Global.TEST_PROFILE_INDEX)
            {
                useControllerUD.Value = device + 1;
                conReadingsUserCon.UseDevice(device, device);
                contReadingsTab.IsEnabled = true;
                controllerReadingsNavigationItem.IsEnabled = true;
            }
            else
            {
                useControllerUD.Value = 1;
                conReadingsUserCon.UseDevice(0, Global.TEST_PROFILE_INDEX);
                contReadingsTab.IsEnabled = true;
                controllerReadingsNavigationItem.IsEnabled = true;
            }

            conReadingsUserCon.EnableControl(false);
            axialLSStickControl.UseDevice(Global.LSModInfo[device]);
            axialRSStickControl.UseDevice(Global.RSModInfo[device]);

            specialActionsVM.LoadActions(currentProfile == null);
            mappingListVM.UpdateMappings();
            profileSettingsVM.UpdateLateProperties();
            profileSettingsVM.PopulateTouchDisInver(touchDisInvertBtn.ContextMenu);
            profileSettingsVM.PopulateGyroMouseTrig(gyroMouseTrigBtn.ContextMenu);
            profileSettingsVM.PopulateGyroMouseStickTrig(gyroMouseStickTrigBtn.ContextMenu);
            profileSettingsVM.PopulateGyroSwipeTrig(gyroSwipeTrigBtn.ContextMenu);
            profileSettingsVM.PopulateGyroControlsTrig(gyroControlsTrigBtn.ContextMenu);
            profileSettingsTabCon.DataContext = profileSettingsVM;
            mappingListBox.DataContext = mappingListVM;
            specialActionsTab.DataContext = specialActionsVM;
            lightbarRect.DataContext = profileSettingsVM;

            StickDeadZoneInfo lsMod = Global.LSModInfo[device];
            if (lsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
            {
                conReadingsUserCon.LsDeadX = profileSettingsVM.LSDeadZone;
                conReadingsUserCon.LsDeadY = profileSettingsVM.LSDeadZone;
            }
            else if (lsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Axial)
            {
                conReadingsUserCon.LsDeadX = axialLSStickControl.AxialVM.DeadZoneX;
                conReadingsUserCon.LsDeadY = axialLSStickControl.AxialVM.DeadZoneY;
            }

            StickDeadZoneInfo rsMod = Global.RSModInfo[device];
            if (rsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Radial)
            {
                conReadingsUserCon.RsDeadX = profileSettingsVM.RSDeadZone;
                conReadingsUserCon.RsDeadY = profileSettingsVM.RSDeadZone;
            }
            else if (rsMod.deadzoneType == StickDeadZoneInfo.DeadZoneType.Axial)
            {
                conReadingsUserCon.RsDeadX = axialRSStickControl.AxialVM.DeadZoneX;
                conReadingsUserCon.RsDeadY = axialRSStickControl.AxialVM.DeadZoneY;
            }

            conReadingsUserCon.L2Dead = profileSettingsVM.L2DeadZone;
            conReadingsUserCon.R2Dead = profileSettingsVM.R2DeadZone;
            conReadingsUserCon.SixAxisXDead = profileSettingsVM.SXDeadZone;
            conReadingsUserCon.SixAxisZDead = profileSettingsVM.SZDeadZone;

            conReadingsUserCon.LsDriftX = profileSettingsVM.LeftStickDriftXAxis;
            conReadingsUserCon.LsDriftY = profileSettingsVM.LeftStickDriftYAxis;
            conReadingsUserCon.RsDriftX = profileSettingsVM.RightStickDriftXAxis;
            conReadingsUserCon.RsDriftY = profileSettingsVM.RightStickDriftYAxis;

            axialLSStickControl.AxialVM.DeadZoneXChanged += UpdateReadingsLsDeadZoneX;
            axialLSStickControl.AxialVM.DeadZoneYChanged += UpdateReadingsLsDeadZoneY;
            axialRSStickControl.AxialVM.DeadZoneXChanged += UpdateReadingsRsDeadZoneX;
            axialRSStickControl.AxialVM.DeadZoneYChanged += UpdateReadingsRsDeadZoneY;

            // Sort special action list by action name
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(specialActionsVM.ActionCol);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription("ActionName", ListSortDirection.Ascending));
            view.Refresh();

            if (profileSettingsVM.UseControllerReadout)
            {
                inputTimer.Start();
            }
        }

        private void StopEditorBindings()
        {
            profileSettingsTabCon.DataContext = null;
            mappingListBox.DataContext = null;
            specialActionsTab.DataContext = null;
            lightbarRect.DataContext = null;

            touchButtonUC.UnregisterDataContext();
            axialLSStickControl.UnregisterDataContext();
            axialRSStickControl.UnregisterDataContext();
        }

        private void RefreshEditorBindings()
        {
            specialActionsVM.LoadActions(currentProfile == null);
            mappingListVM.UpdateMappings();
            profileSettingsVM.UpdateLateProperties();
            profileSettingsVM.PopulateTouchDisInver(touchDisInvertBtn.ContextMenu);
            profileSettingsVM.PopulateGyroMouseTrig(gyroMouseTrigBtn.ContextMenu);
            profileSettingsVM.PopulateGyroMouseStickTrig(gyroMouseStickTrigBtn.ContextMenu);
            profileSettingsVM.PopulateGyroSwipeTrig(gyroSwipeTrigBtn.ContextMenu);
            profileSettingsVM.PopulateGyroControlsTrig(gyroControlsTrigBtn.ContextMenu);
            profileSettingsTabCon.DataContext = profileSettingsVM;
            mappingListBox.DataContext = mappingListVM;
            specialActionsTab.DataContext = specialActionsVM;
            lightbarRect.DataContext = profileSettingsVM;

            conReadingsUserCon.LsDeadX = profileSettingsVM.LSDeadZone;
            conReadingsUserCon.RsDeadX = profileSettingsVM.RSDeadZone;
            conReadingsUserCon.L2Dead = profileSettingsVM.L2DeadZone;
            conReadingsUserCon.R2Dead = profileSettingsVM.R2DeadZone;
            conReadingsUserCon.SixAxisXDead = profileSettingsVM.SXDeadZone;
            conReadingsUserCon.SixAxisZDead = profileSettingsVM.SZDeadZone;
            conReadingsUserCon.LsDriftX = profileSettingsVM.LeftStickDriftXAxis;
            conReadingsUserCon.LsDriftY = profileSettingsVM.LeftStickDriftYAxis;
            conReadingsUserCon.RsDriftX = profileSettingsVM.RightStickDriftXAxis;
            conReadingsUserCon.RsDriftY = profileSettingsVM.RightStickDriftYAxis;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            ClearRumblePreview();

            Global.outDevTypeTemp[deviceNum] = OutContType.ViiperX360;
            Mapping.RequestRegularProfileReload(deviceNum, false, App.rootHub);

            Closed?.Invoke(this, EventArgs.Empty);
        }

        private void HoverConBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                !hoverIndexes.TryGetValue(button, out int mappingIndex) ||
                mappingIndex < 0 || mappingIndex >= mappingListVM.Mappings.Count ||
                !mappingListVM.Mappings[mappingIndex].IsAvailableOnPhysicalController)
            {
                return;
            }

            mappingListVM.SelectedIndex = mappingIndex;
            MappedControl mpControl = mappingListVM.Mappings[mappingListVM.SelectedIndex];
            BindingWindow window = new BindingWindow(deviceNum, mpControl.Setting);
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
            mpControl.UpdateMappingName();
            UpdateHighlightLabel(mpControl);
            Global.CacheProfileCustomsFlags(profileSettingsVM.Device);
        }

        private void InputControlHighlight(Button control)
        {
            controllerVectorHighlight.Visibility = Visibility.Collapsed;
            picBoxHover.Visibility = Visibility.Hidden;

            if (controllerHoverImages.TryGetValue(control, out ImageSource controllerHover))
            {
                picBoxHover.Source = controllerHover;
                Canvas.SetLeft(picBoxHover, 0);
                Canvas.SetTop(picBoxHover, 0);
                picBoxHover.Width = 440;
                picBoxHover.Height = 220;
                picBoxHover.Stretch = Stretch.Fill;
                picBoxHover.Visibility = Visibility.Visible;
            }
            else if (vectorHoverGeometries.TryGetValue(control, out Geometry geometry))
            {
                controllerVectorHighlight.Data = TransformControllerGeometry(geometry);
                controllerVectorHighlight.Visibility = Visibility.Visible;
            }
            else
            {
                if (hoverImages.TryGetValue(control, out ImageBrush tempBrush))
                {
                    picBoxHover.Source = tempBrush.ImageSource;
                }

                if (hoverLocations.TryGetValue(control, out HoverImageInfo tempInfo))
                {
                    Canvas.SetLeft(picBoxHover, tempInfo.point.X);
                    Canvas.SetTop(picBoxHover, tempInfo.point.Y);
                    picBoxHover.Width = tempInfo.size.Width;
                    picBoxHover.Height = tempInfo.size.Height;
                    picBoxHover.Stretch = Stretch.Fill;
                    picBoxHover.Visibility = Visibility.Visible;
                }
            }

            if (hoverIndexes.TryGetValue(control, out int tempIndex))
            {
                mappingListVM.SelectedIndex = tempIndex;
                mappingListBox.ScrollIntoView(mappingListBox.SelectedItem);
                MappedControl mapped = mappingListVM.Mappings[tempIndex];
                UpdateHighlightLabel(mapped);
            }
        }

        private void UpdateHighlightLabel(MappedControl mapped)
        {
            string display = $"{mapped.ControlName}: {mapped.MappingName}";
            if (mapped.HasShiftAction())
            {
                display += "\nShift: ";
                display += mapped.ShiftMappingName;
            }

            highlightControlDisplayLb.Content = display;
        }

        private void ContBtn_MouseEnter(object sender, MouseEventArgs e)
        {
            Button control = sender as Button;
            InputControlHighlight(control);
        }

        private void ContBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            HideControllerHover();
        }

        private void HideControllerHover()
        {
            Canvas.SetLeft(picBoxHover, 0);
            Canvas.SetTop(picBoxHover, 0);
            picBoxHover.Visibility = Visibility.Hidden;
            controllerVectorHighlight.Visibility = Visibility.Collapsed;
        }

        private void GyroOutModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = gyroOutModeCombo.SelectedIndex;
            if (idx >= 0)
            {
                if (deviceNum < ControlService.CURRENT_DS4_CONTROLLER_LIMIT)
                {
                    App.rootHub.touchPad[deviceNum]?.ResetToggleGyroModes();
                }
            }
        }

        private void SetLateProperties(bool fullSave = true)
        {
            Global.BTPollRate[deviceNum] = profileSettingsVM.TempBTPollRateIndex;
            Global.OutContType[deviceNum] = profileSettingsVM.TempConType;
            if (fullSave)
            {
                Global.outDevTypeTemp[deviceNum] = OutContType.ViiperX360;
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (profileSettingsVM.UseDs3PitchRollSim)
            {
                // change controller type to DS4 if the DS3 pitch and roll sim is on
                profileSettingsVM.TempControllerIndex = 1;
            }

            if (profileSettingsVM.HasUseDs3PitchRollSimChanged)
            {
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                if (mainWindow is not null)
                {
                    var changeServiceTask = Task.Run(() => Dispatcher.InvokeAsync(mainWindow.ChangeService));
                    changeServiceTask.ContinueWith(_ => Dispatcher.InvokeAsync(() => mainWindow.ChangeService()));

                }
                else
                {
                    MessageBox.Show("The app has to be restarted for DS3 gyro simulation to work.",
                        ProductIdentity.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            bool saved = ApplyProfileStep(false);
            if (saved)
            {
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool ApplyProfileStep(bool fullSave = true)
        {
            bool result = false;
            ClearRumblePreview();

            if (profileSettingsVM.HasDebouncingMsChanged)
            {
                Global.DebouncingMsHasChanged();
            }

            string temp = profileNameTxt.Text;
            if (!string.IsNullOrWhiteSpace(temp) &&
                temp.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) == -1)
            {
                SetLateProperties(false);
                DS4Windows.Global.ProfilePath[deviceNum] =
                    DS4Windows.Global.OlderProfilePath[deviceNum] = temp;

                if (currentProfile != null)
                {
                    if (temp != currentProfile.Name)
                    {
                        //File.Delete(DS4Windows.Global.appdatapath + @"\Profiles\" + currentProfile.Name + ".xml");
                        currentProfile.DeleteFile();
                        currentProfile.Name = temp;
                    }
                }

                if (currentProfile != null)
                {
                    currentProfile.SaveProfile(deviceNum);
                    currentProfile.FireSaved();
                    result = true;
                }
                else
                {
                    string tempprof = Global.appdatapath + @"\Profiles\" + temp + ".xml";
                    if (!File.Exists(tempprof))
                    {
                        Global.SaveProfile(deviceNum, temp);
                        CreatedProfile?.Invoke(this, temp);
                        result = true;
                    }
                    else
                    {
                        MessageBox.Show(Properties.Resources.ValidName, Properties.Resources.NotValid,
                            MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.ValidName, Properties.Resources.NotValid,
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

            return result;
        }

        public void Close()
        {
            ClearRumblePreview();

            Closed?.Invoke(this, EventArgs.Empty);
        }

        private void ColorByBatteryPerCk_Click(object sender, RoutedEventArgs e)
        {
            ColorByBatteryPerCheck();
        }

        private void ColorByBatteryPerCheck()
        {
            bool state = profileSettingsVM.ColorBatteryPercent;
            if (state)
            {
                colorGB.Header = Translations.Strings.Full;
                emptyColorGB.Visibility = Visibility.Visible;
            }
            else
            {
                colorGB.Header = Translations.Strings.Color;
                // Collapsed, not Hidden: a hidden group still reserves its whole
                // band, which left a large empty gap in the middle of the section
                // whenever battery colouring was off.
                emptyColorGB.Visibility = Visibility.Collapsed;
            }
        }

        private void FlashColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerWindow dialog = new ColorPickerWindow();
            dialog.Owner = Application.Current.MainWindow;
            Color tempcolor = profileSettingsVM.FlashColorMedia;
            dialog.colorPicker.SelectedColor = tempcolor;
            profileSettingsVM.StartForcedColor(tempcolor);
            dialog.ColorChanged += (sender2, color) => { profileSettingsVM.UpdateForcedColor(color); };
            dialog.ShowDialog();
            profileSettingsVM.EndForcedColor();
            profileSettingsVM.UpdateFlashColor(dialog.colorPicker.SelectedColor.GetValueOrDefault());
        }

        private void LowColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerWindow dialog = new ColorPickerWindow();
            dialog.Owner = Application.Current.MainWindow;
            Color tempcolor = profileSettingsVM.LowColorMedia;
            dialog.colorPicker.SelectedColor = tempcolor;
            profileSettingsVM.StartForcedColor(tempcolor);
            dialog.ColorChanged += (sender2, color) => { profileSettingsVM.UpdateForcedColor(color); };
            dialog.ShowDialog();
            profileSettingsVM.EndForcedColor();
            profileSettingsVM.UpdateLowColor(dialog.colorPicker.SelectedColor.GetValueOrDefault());
        }

        private enum RumbleType
        {
            Light,
            Heavy
        }

        private void RumbleTestBtn_Click(object sender, RoutedEventArgs e)
        {
            int deviceNum = ResolveControllerContextIndex(
                profileSettingsVM.Device, triggerPreviewDeviceIndex);
            if (deviceNum >= 0 &&
                deviceNum < ControlService.CURRENT_DS4_CONTROLLER_LIMIT)
            {
                DS4Device d = App.rootHub.DS4Controllers[deviceNum];
                if (d != null)
                {
                    RumbleType type;
                    var btn = sender as Button;
                    if (!profileSettingsVM.InverseRumbleMotors)
                    {
                        type = btn.Name == "heavyRumbleTestBtn" ? RumbleType.Heavy : RumbleType.Light;
                    }
                    else
                    {
                        type = btn.Name == "heavyRumbleTestBtn" ? RumbleType.Light : RumbleType.Heavy;
                    }

                    bool rumbleActive;
                    if (type == RumbleType.Heavy)
                        rumbleActive = profileSettingsVM.HeavyRumbleActive;
                    else
                        rumbleActive = profileSettingsVM.LightRumbleActive;
                    if (!rumbleActive)
                    {
                        var rumbleBoost = profileSettingsVM.RumbleBoost;

                        if (type == RumbleType.Heavy)
                            profileSettingsVM.HeavyRumbleActive = true;
                        else
                            profileSettingsVM.LightRumbleActive = true;

                        PublishRumblePreview(d, rumbleBoost);

                        if (type == RumbleType.Heavy)
                        {
                            if (!profileSettingsVM.InverseRumbleMotors)
                                heavyRumbleTestBtn.Content = Properties.Resources.StopHText;
                            else
                                lightRumbleTestBtn.Content = Properties.Resources.StopLText;
                        }
                        else
                        {
                            if (!profileSettingsVM.InverseRumbleMotors)
                                lightRumbleTestBtn.Content = Properties.Resources.StopLText;
                            else
                                heavyRumbleTestBtn.Content = Properties.Resources.StopHText;
                        }
                    }
                    else
                    {
                        if (type == RumbleType.Heavy)
                        {
                            profileSettingsVM.HeavyRumbleActive = false;
                            PublishRumblePreview(d,
                                ResolvePreviewRumbleBoost(d));
                            if (!profileSettingsVM.InverseRumbleMotors)
                                heavyRumbleTestBtn.Content = Properties.Resources.TestHText;
                            else
                                lightRumbleTestBtn.Content = Properties.Resources.TestLText;
                        }
                        else
                        {
                            profileSettingsVM.LightRumbleActive = false;
                            PublishRumblePreview(d,
                                ResolvePreviewRumbleBoost(d));
                            if (!profileSettingsVM.InverseRumbleMotors)
                                lightRumbleTestBtn.Content = Properties.Resources.TestLText;
                            else
                                heavyRumbleTestBtn.Content = Properties.Resources.TestHText;
                        }
                    }
                }
            }
        }

        private void PublishRumblePreview(DS4Device device, int rumbleBoost)
        {
            byte strength = (byte)Math.Min(255,
                255 * Math.Max(0, rumbleBoost) / 100);
            device.SetRumblePreview(
                profileSettingsVM.LightRumbleActive, strength,
                profileSettingsVM.HeavyRumbleActive, strength);
        }

        private int ResolvePreviewRumbleBoost(DS4Device device)
        {
            return profileSettingsVM.RumbleBoost;
        }

        private void ClearRumblePreview()
        {
            int controllerIndex = ResolveControllerContextIndex(
                profileSettingsVM.Device, triggerPreviewDeviceIndex);
            if (controllerIndex >= 0 &&
                controllerIndex < ControlService.CURRENT_DS4_CONTROLLER_LIMIT)
            {
                App.rootHub.DS4Controllers[controllerIndex]?.
                    ClearRumblePreview();
            }

            profileSettingsVM.HeavyRumbleActive = false;
            profileSettingsVM.LightRumbleActive = false;
        }

        private void CustomEditorBtn_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string tag = btn.Tag.ToString();
            if (tag == "LS") LaunchCurveEditor(profileSettingsVM.LSCustomCurve);
            else if (tag == "RS") LaunchCurveEditor(profileSettingsVM.RSCustomCurve);
            else if (tag == "L2") LaunchCurveEditor(profileSettingsVM.L2CustomCurve);
            else if (tag == "R2") LaunchCurveEditor(profileSettingsVM.R2CustomCurve);
            else if (tag == "SX") LaunchCurveEditor(profileSettingsVM.SXCustomCurve);
            else if (tag == "SZ") LaunchCurveEditor(profileSettingsVM.SZCustomCurve);
        }

        private void LaunchCurveEditor(string customDefinition)
        {
            profileSettingsVM.LaunchCurveEditor(customDefinition);
        }

        private void LaunchProgBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            dialog.AddExtension = true;
            dialog.DefaultExt = ".exe";
            dialog.Filter = "Program (*.exe)|*.exe";
            dialog.Title = "Select Program";

            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (dialog.ShowDialog() == true)
            {
                profileSettingsVM.UpdateLaunchProgram(dialog.FileName);
            }
        }

        private void FrictionUD_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (deviceNum < ControlService.CURRENT_DS4_CONTROLLER_LIMIT)
            {
                App.rootHub.touchPad[deviceNum]?.ResetTrackAccel(frictionUD.Value.GetValueOrDefault());
            }
        }

        private void RainbowBtn_Click(object sender, RoutedEventArgs e)
        {
            bool active = profileSettingsVM.Rainbow != 0.0;
            if (active)
            {
                profileSettingsVM.Rainbow = 0.0;
                colorByBatteryPerCk.Content = Properties.Resources.ColorByBattery;
                colorGB.IsEnabled = true;
                emptyColorGB.IsEnabled = true;
            }
            else
            {
                profileSettingsVM.Rainbow = 5.0;
                colorByBatteryPerCk.Content = Properties.Resources.DimByBattery;
                colorGB.IsEnabled = false;
                emptyColorGB.IsEnabled = false;
            }
        }

        private void ChargingColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerWindow dialog = new ColorPickerWindow();
            dialog.Owner = Application.Current.MainWindow;
            Color tempcolor = profileSettingsVM.ChargingColorMedia;
            dialog.colorPicker.SelectedColor = tempcolor;
            profileSettingsVM.StartForcedColor(tempcolor);
            dialog.ColorChanged += (sender2, color) =>
            {
                profileSettingsVM.UpdateForcedColor(color);
            };
            dialog.ShowDialog();
            profileSettingsVM.EndForcedColor();
            profileSettingsVM.UpdateChargingColor(dialog.colorPicker.SelectedColor.GetValueOrDefault());
        }

        private void SteeringWheelEmulationCalibrateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (profileSettingsVM.SASteeringWheelEmulationAxisIndex > 0)
            {
                DS4Windows.DS4Device d = App.rootHub.DS4Controllers[profileSettingsVM.FuncDevNum];
                if (d != null)
                {
                    System.Drawing.Point origWheelCenterPoint = new System.Drawing.Point(d.wheelCenterPoint.X, d.wheelCenterPoint.Y);
                    System.Drawing.Point origWheel90DegPointLeft = new System.Drawing.Point(d.wheel90DegPointLeft.X, d.wheel90DegPointLeft.Y);
                    System.Drawing.Point origWheel90DegPointRight = new System.Drawing.Point(d.wheel90DegPointRight.X, d.wheel90DegPointRight.Y);

                    d.WheelRecalibrateActiveState = 1;

                    MessageBoxResult result = MessageBox.Show($"{Properties.Resources.SASteeringWheelEmulationCalibrate}.\n\n" +
                            $"{Properties.Resources.SASteeringWheelEmulationCalibrateInstruction1}.\n" +
                            $"{Properties.Resources.SASteeringWheelEmulationCalibrateInstruction2}.\n" +
                            $"{Properties.Resources.SASteeringWheelEmulationCalibrateInstruction3}.\n\n" +
                            $"{Properties.Resources.SASteeringWheelEmulationCalibrateInstruction}.\n",
                        Properties.Resources.SASteeringWheelEmulationCalibrate, MessageBoxButton.OKCancel, MessageBoxImage.Information, MessageBoxResult.OK);

                    if (result == MessageBoxResult.OK)
                    {
                        // Accept new calibration values (State 3 is "Complete calibration" state)
                        d.WheelRecalibrateActiveState = 3;
                    }
                    else
                    {
                        // Cancel calibration and reset back to original calibration values
                        d.WheelRecalibrateActiveState = 4;

                        d.wheelFullTurnCount = 0;
                        d.wheelCenterPoint = origWheelCenterPoint;
                        d.wheel90DegPointLeft = origWheel90DegPointLeft;
                        d.wheel90DegPointRight = origWheel90DegPointRight;
                    }
                }
                else
                {
                    MessageBox.Show($"{Properties.Resources.SASteeringWheelEmulationCalibrateNoControllerError}.");
                }
            }
        }

        private void TouchDisInvertBtn_Click(object sender, RoutedEventArgs e)
        {
            touchDisInvertBtn.ContextMenu.IsOpen = true;
        }

        private void TouchDisInvertMenuItem_Click(object sender, RoutedEventArgs e)
        {
            profileSettingsVM.UpdateTouchDisInvert(touchDisInvertBtn.ContextMenu);
        }

        private void GyroMouseTrigMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = gyroMouseTrigBtn.ContextMenu;
            int itemCount = menu.Items.Count;
            MenuItem alwaysOnItem = menu.Items[itemCount - 1] as MenuItem;

            profileSettingsVM.UpdateGyroMouseTrig(menu, e.OriginalSource == alwaysOnItem);
        }

        private void GyroMouseStickTrigMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = gyroMouseStickTrigBtn.ContextMenu;
            int itemCount = menu.Items.Count;
            MenuItem alwaysOnItem = menu.Items[itemCount - 1] as MenuItem;

            profileSettingsVM.UpdateGyroMouseStickTrig(menu, e.OriginalSource == alwaysOnItem);
        }

        private void GyroMouseTrigBtn_Click(object sender, RoutedEventArgs e)
        {
            gyroMouseTrigBtn.ContextMenu.IsOpen = true;
        }

        private void GyroMouseStickTrigBtn_Click(object sender, RoutedEventArgs e)
        {
            gyroMouseStickTrigBtn.ContextMenu.IsOpen = true;
        }

        private void OutConTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = outConTypeCombo.SelectedIndex;
            if (index >= 0)
            {
                if (ViiperSetupManager.IsViiperOutputType(profileSettingsVM.TempConType))
                {
                    ViiperSetupManager.EnsureReadyWithPrompt(Application.Current.MainWindow);
                }

                UpdateOutputControllerHint(profileSettingsVM.TempConType);
                mappingListVM.UpdateMappingDevType(profileSettingsVM.TempConType);
            }
        }

        private void UpdateOutputControllerHint(OutContType type)
        {
            if (outputControllerHintText == null)
            {
                return;
            }

            outputControllerHintText.Text = type switch
            {
                OutContType.ViiperDualSense =>
                    "DualSense output exposes native DualSense identity with mute, touch, gyro, rumble, lightbar, and player LEDs. Adaptive triggers require the raw-output follow-up.",
                OutContType.ViiperDualSenseEdge =>
                    "DualSense Edge output exposes native Edge identity with mute, touch, gyro, Fn buttons, back paddles, rumble, lightbar, and player LEDs. Adaptive triggers require the raw-output follow-up.",
                OutContType.ViiperDS4 =>
                    "DualShock 4 output provides DS4 buttons, touch, gyro, rumble, lightbar, and flash feedback.",
                OutContType.ViiperX360 =>
                    "Xbox 360 output provides standard XInput-style buttons, sticks, triggers, and rumble.",
                _ => string.Empty,
            };
        }

        private void NewActionBtn_Click(object sender, RoutedEventArgs e)
        {
            baseSpeActPanel.Visibility = Visibility.Collapsed;
            ProfileList profList = (Application.Current.MainWindow as MainWindow).ProfileListHolder;
            SpecialActionEditor actEditor = new SpecialActionEditor(deviceNum, profList, null);
            specialActionDockPanel.Children.Add(actEditor);
            actEditor.Visibility = Visibility.Visible;
            actEditor.Cancel += (sender2, args) =>
            {
                specialActionDockPanel.Children.Remove(actEditor);
                baseSpeActPanel.Visibility = Visibility.Visible;
            };
            actEditor.Saved += (sender2, actionName) =>
            {
                SpecialAction action = Global.GetAction(actionName);
                SpecialActionItem newitem = specialActionsVM.CreateActionItem(action);
                newitem.Active = true;
                int lastIdx = specialActionsVM.ActionCol.Count;
                newitem.Index = lastIdx;
                specialActionsVM.ActionCol.Add(newitem);
                specialActionDockPanel.Children.Remove(actEditor);
                baseSpeActPanel.Visibility = Visibility.Visible;

                specialActionsVM.ExportEnabledActions();
                Global.CacheExtraProfileInfo(profileSettingsVM.Device);
            };
        }

        private void EditActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (specialActionsVM.SpecialActionIndex >= 0)
            {
                SpecialActionItem item = specialActionsVM.CurrentSpecialActionItem;
                int currentIndex = item.Index;
                //int viewIndex = specialActionsVM.SpecialActionIndex;
                //int currentIndex = specialActionsVM.ActionCol[viewIndex].Index;
                //SpecialActionItem item = specialActionsVM.ActionCol[currentIndex];
                baseSpeActPanel.Visibility = Visibility.Collapsed;
                ProfileList profList = (Application.Current.MainWindow as MainWindow).ProfileListHolder;
                SpecialActionEditor actEditor = new SpecialActionEditor(deviceNum, profList, item.SpecialAction);
                specialActionDockPanel.Children.Add(actEditor);
                actEditor.Visibility = Visibility.Visible;
                actEditor.Cancel += (sender2, args) =>
                {
                    specialActionDockPanel.Children.Remove(actEditor);
                    baseSpeActPanel.Visibility = Visibility.Visible;
                };
                actEditor.Saved += (sender2, actionName) =>
                {
                    DS4Windows.SpecialAction action = DS4Windows.Global.GetAction(actionName);
                    SpecialActionItem newitem = specialActionsVM.CreateActionItem(action);
                    newitem.Active = item.Active;
                    newitem.Index = currentIndex;
                    specialActionsVM.ActionCol.RemoveAt(currentIndex);
                    specialActionsVM.ActionCol.Insert(currentIndex, newitem);
                    specialActionDockPanel.Children.Remove(actEditor);
                    baseSpeActPanel.Visibility = Visibility.Visible;
                    Global.CacheExtraProfileInfo(profileSettingsVM.Device);
                };
            }
        }

        private void RemoveActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (specialActionsVM.SpecialActionIndex >= 0)
            {
                SpecialActionItem item = specialActionsVM.CurrentSpecialActionItem;
                //int currentIndex = specialActionsVM.ActionCol[specialActionsVM.SpecialActionIndex].Index;
                //SpecialActionItem item = specialActionsVM.ActionCol[currentIndex];
                specialActionsVM.RemoveAction(item);
                Global.CacheExtraProfileInfo(profileSettingsVM.Device);
            }
        }

        private void SpecialActionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            specialActionsVM.ExportEnabledActions();
        }

        private void Ds4LightbarColorBtn_MouseEnter(object sender, MouseEventArgs e)
        {
            highlightControlDisplayLb.Content = "Click the lightbar for color picker";
        }

        private void Ds4LightbarColorBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            highlightControlDisplayLb.Content = "";
        }

        private void Ds4LightbarColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerWindow dialog = new ColorPickerWindow();
            dialog.Owner = Application.Current.MainWindow;
            Color tempcolor = profileSettingsVM.MainColor;
            dialog.colorPicker.SelectedColor = tempcolor;
            profileSettingsVM.StartForcedColor(tempcolor);
            dialog.ColorChanged += (sender2, color) =>
            {
                profileSettingsVM.UpdateForcedColor(color);
            };
            dialog.ShowDialog();
            profileSettingsVM.EndForcedColor();
            profileSettingsVM.UpdateMainColor(dialog.colorPicker.SelectedColor.GetValueOrDefault());
        }

        private void InputDS4(object sender, System.Timers.ElapsedEventArgs e)
        {
            inputTimer.Stop();

            bool activeWin = false;
            int tempDeviceNum = 0;
            Dispatcher.Invoke(() =>
            {
                activeWin = Application.Current.MainWindow.IsActive;
                tempDeviceNum = profileSettingsVM.FuncDevNum;
            });

            if (activeWin && profileSettingsVM.UseControllerReadout)
            {
                int index = -1;
                switch(Program.rootHub.GetActiveInputControl(tempDeviceNum))
                {
                    case DS4Controls.None: break;
                    case DS4Controls.Cross: index = 0; break;
                    case DS4Controls.Circle: index = 1; break;
                    case DS4Controls.Square: index = 2; break;
                    case DS4Controls.Triangle: index = 3; break;
                    case DS4Controls.Options: index = 4; break;
                    case DS4Controls.Share: index = 5; break;
                    case DS4Controls.DpadUp: index = 6; break;
                    case DS4Controls.DpadDown: index = 7; break;
                    case DS4Controls.DpadLeft: index = 8; break;
                    case DS4Controls.DpadRight: index = 9; break;
                    case DS4Controls.PS: index = 10; break;
                    case DS4Controls.Mute: index = 11; break;
                    case DS4Controls.L1: index = 12; break;
                    case DS4Controls.R1: index = 13; break;
                    case DS4Controls.L2: index = 14; break;
                    case DS4Controls.R2: index = 15; break;
                    case DS4Controls.L3: index = 16; break;
                    case DS4Controls.R3: index = 17; break;
                    case DS4Controls.TouchLeft: index = 18; break;
                    case DS4Controls.TouchRight: index = 19; break;
                    case DS4Controls.TouchMulti: index = 20; break;
                    case DS4Controls.TouchUpper: index = 21; break;
                    case DS4Controls.LYNeg: index = 22; break;
                    case DS4Controls.LYPos: index = 23; break;
                    case DS4Controls.LXNeg: index = 24; break;
                    case DS4Controls.LXPos: index = 25; break;
                    case DS4Controls.RYNeg: index = 26; break;
                    case DS4Controls.RYPos: index = 27; break;
                    case DS4Controls.RXNeg: index = 28; break;
                    case DS4Controls.RXPos: index = 29; break;
                    case DS4Controls.FnL: index = 30; break;
                    case DS4Controls.FnR: index = 31; break;
                    case DS4Controls.BLP: index = 32; break;
                    case DS4Controls.BRP: index = 33; break;
                    default: break;
                }

                if (index >= 0)
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        mappingListVM.SelectedIndex = index;
                        ShowControlBindingWindow();
                    }));
                }
            }

            if (profileSettingsVM.UseControllerReadout)
            {
                inputTimer.Start();
            }
        }
        private void ProfileEditor_Closed(object sender, EventArgs e)
        {
            profileSettingsVM.UseControllerReadout = false;
            inputTimer.Stop();
            conReadingsUserCon.EnableControl(false);
            Global.CacheExtraProfileInfo(profileSettingsVM.Device);
            UnregisterEvents();
        }

        private void UseControllerReadoutCk_Click(object sender, RoutedEventArgs e)
        {
            if (profileSettingsVM.UseControllerReadout && profileSettingsVM.Device < ControlService.CURRENT_DS4_CONTROLLER_LIMIT)
            {
                inputTimer.Start();
            }
            else
            {
                inputTimer.Stop();
            }
        }

        private void ShowControlBindingWindow()
        {
            MappedControl mpControl = mappingListVM.Mappings[mappingListVM.SelectedIndex];
            BindingWindow window = new BindingWindow(deviceNum, mpControl.Setting);
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
            mpControl.UpdateMappingName();
            UpdateHighlightLabel(mpControl);
            Global.CacheProfileCustomsFlags(profileSettingsVM.Device);
        }

        private void EditSelectedMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            int index = mappingListBox.SelectedIndex;
            if (index < 0 || index >= mappingListVM.Mappings.Count ||
                !mappingListVM.Mappings[index].IsAvailableOnPhysicalController)
            {
                return;
            }

            mappingListVM.SelectedIndex = index;
            ShowControlBindingWindow();
        }

        private void MappingListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            IInputElement hitElement = mappingListBox.InputHitTest(
                e.GetPosition(mappingListBox));
            ListBoxItem clickedItem = ItemsControl.ContainerFromElement(
                mappingListBox, hitElement as DependencyObject) as ListBoxItem;
            if (clickedItem == null || !clickedItem.IsEnabled)
            {
                return;
            }

            int clickedIndex = mappingListBox.ItemContainerGenerator
                .IndexFromContainer(clickedItem);
            if (clickedIndex < 0)
            {
                return;
            }

            mappingListVM.SelectedIndex = clickedIndex;
            e.Handled = true;
            ShowControlBindingWindow();
        }

        private void SidebarTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sidebarTabControl.SelectedItem == contReadingsTab)
            {
                controllerReadingsTabActive = true;
                conReadingsUserCon.EnableControl(true);
            }
            else if (controllerReadingsTabActive)
            {
                controllerReadingsTabActive = false;
                conReadingsUserCon.EnableControl(false);
            }
        }

        private void TiltControlsButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            DS4Controls control = (DS4Controls)Convert.ToInt32(btn.Tag);
            MappedControl mpControl = mappingListVM.ControlMap[control];
            BindingWindow window = new BindingWindow(deviceNum, mpControl.Setting);
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
            mpControl.UpdateMappingName();
            UpdateHighlightLabel(mpControl);
            Global.CacheProfileCustomsFlags(profileSettingsVM.Device);
        }

        private void SwipeControlsButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            DS4Controls control = (DS4Controls)Convert.ToInt32(btn.Tag);
            MappedControl mpControl = mappingListVM.ControlMap[control];
            BindingWindow window = new BindingWindow(deviceNum, mpControl.Setting);
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
            mpControl.UpdateMappingName();
            UpdateHighlightLabel(mpControl);
            Global.CacheProfileCustomsFlags(profileSettingsVM.Device);
        }

        private void ConBtn_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button btn ||
                !hoverIndexes.TryGetValue(btn, out int mappingIndex) ||
                mappingIndex < 0 || mappingIndex >= mappingListVM.Mappings.Count ||
                !mappingListVM.Mappings[mappingIndex].IsAvailableOnPhysicalController)
            {
                return;
            }

            mappingListVM.SelectedIndex = mappingIndex;
            MappedControl mpControl = mappingListVM.Mappings[mappingIndex];
            profileSettingsVM.PresetMenuUtil.SetHighlightControl(mpControl.Control);
            ContextMenu cm = conCanvas.FindResource("presetMenu") as ContextMenu;
            MenuItem temp = cm.Items[0] as MenuItem;
            temp.Header = profileSettingsVM.PresetMenuUtil.PresetInputLabel;
            cm.PlacementTarget = btn;
            cm.IsOpen = true;
        }

        private void PresetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            int baseTag = Convert.ToInt32(item.Tag);
            int subTag = Convert.ToInt32(item.CommandParameter);
            if (baseTag >= 0 && subTag >= 0)
            {
                List<DS4Controls> controls =
                    profileSettingsVM.PresetMenuUtil.ModifySettingWithPreset(baseTag, subTag);
                foreach(DS4Controls control in controls)
                {
                    MappedControl mpControl = mappingListVM.ControlMap[control];
                    mpControl.UpdateMappingName();
                }

                Global.CacheProfileCustomsFlags(profileSettingsVM.Device);
                highlightControlDisplayLb.Content = "";
            }
        }

        private void PresetBtn_Click(object sender, RoutedEventArgs e)
        {
            sidebarTabControl.SelectedIndex = 0;

            PresetOptionWindow presetWin = new PresetOptionWindow();
            presetWin.SetupData(deviceNum);
            presetWin.ToPresetsScreen();
            presetWin.DelayPresetApply = true;
            presetWin.ShowDialog();

            if (presetWin.Result == MessageBoxResult.OK)
            {
                StopEditorBindings();
                presetWin.ApplyPreset();
                RefreshEditorBindings();
            }
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyProfileStep();
        }

        private void GyroCalibration_Click(object sender, RoutedEventArgs e)
        {
            int deviceNum = profileSettingsVM.FuncDevNum;
            if (deviceNum < ControlService.CURRENT_DS4_CONTROLLER_LIMIT)
            {
                DS4Device d = App.rootHub.DS4Controllers[deviceNum];
                d.SixAxis.ResetContinuousCalibration();
                if (d.JointDeviceSlotNumber != DS4Device.DEFAULT_JOINT_SLOT_NUMBER)
                {
                    DS4Device tempDev = App.rootHub.DS4Controllers[d.JointDeviceSlotNumber];
                    tempDev?.SixAxis.ResetContinuousCalibration();
                }
            }
        }

        private void GyroSwipeTrigBtn_Click(object sender, RoutedEventArgs e)
        {
            gyroSwipeTrigBtn.ContextMenu.IsOpen = true;
        }

        private void GyroSwipeTrigMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = gyroSwipeTrigBtn.ContextMenu;
            int itemCount = menu.Items.Count;
            MenuItem alwaysOnItem = menu.Items[itemCount - 1] as MenuItem;

            profileSettingsVM.UpdateGyroSwipeTrig(menu, e.OriginalSource == alwaysOnItem);
        }

        private void GyroSwipeControlsBtn_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            DS4Controls control = (DS4Controls)Convert.ToInt32(btn.Tag);
            MappedControl mpControl = mappingListVM.ControlMap[control];
            BindingWindow window = new BindingWindow(deviceNum, mpControl.Setting);
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
            mpControl.UpdateMappingName();
            Global.CacheProfileCustomsFlags(profileSettingsVM.Device);
        }

        private void GyroControlsTrigBtn_Click(object sender, RoutedEventArgs e)
        {
            gyroControlsTrigBtn.ContextMenu.IsOpen = true;
        }

        private void GyroControlsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = gyroControlsTrigBtn.ContextMenu;
            int itemCount = menu.Items.Count;
            MenuItem alwaysOnItem = menu.Items[itemCount - 1] as MenuItem;

            profileSettingsVM.UpdateGyroControlsTrig(menu, e.OriginalSource == alwaysOnItem);
        }

        private void StickOuterBindButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int tag = Convert.ToInt32(btn.Tag);
            DS4Controls ds4control = (DS4Controls)tag;
            if (ds4control == DS4Controls.None)
            {
                return;
            }

            //DS4ControlSettings setting = Global.getDS4CSetting(tag, ds4control);
            MappedControl mpControl = mappingListVM.ControlMap[ds4control];
            BindingWindow window = new BindingWindow(deviceNum, mpControl.Setting);
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
            mpControl.UpdateMappingName();
            Global.CacheProfileCustomsFlags(profileSettingsVM.Device);
        }

        private void CalibrateStick_OnClick(object sender, RoutedEventArgs e)
        {
            if (deviceNum == 8)
            {
                MessageBox.Show("Stick recalibration is only available if the profile editor is opened " +
                                "with the Edit button next to the controller you want to recalibrate in the main " +
                                "window.",
                    ProductIdentity.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var button = sender as Button;
            var tag = Convert.ToInt32(button?.Tag);
            var stick = tag switch
            {
                0 => Stick.Left,
                1 => Stick.Right,
                _ => throw new IndexOutOfRangeException("Wrong stick index. Must be 0 for left or 1 for right.")
            };

            StickCalibrationWindow window = new(stick, deviceNum, profileSettingsVM)
            {
                Owner = Application.Current.MainWindow,
            };
            window.ShowDialog();
        }

        private void ExportSpecialActions_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.AddExtension = true;
            dialog.DefaultExt = ".xml";
            dialog.FileName = "Actions";
            dialog.Filter = $"{ProductIdentity.Name} Special Actions (*.xml)|*.xml";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var profileStream = new StreamReader(@$"{Global.appdatapath}\Actions.xml").BaseStream;
                var dialogStream = dialog.OpenFile();
                profileStream.CopyTo(dialogStream);
                profileStream.Close();
                dialogStream.Close();
            }
        }
    }

    public class ResourcePaths
    {
        public string SizePNG { get => $"{Global.RESOURCES_PREFIX}/size.png"; }
        public string DS4ConfigPNG { get => $"{Global.RESOURCES_PREFIX}/DS4 Config.png"; }
        public string DS4LightbarPNG { get => $"{Global.RESOURCES_PREFIX}/DS4 lightbar.png"; }
        public string DS4ConfigRSPNG { get => $"{Global.RESOURCES_PREFIX}/DS4-Config_RS.png"; }
        public string RainbowPNG { get => $"{Global.RESOURCES_PREFIX}/rainbow.png"; }
    }

    public class ControlIndexCheck
    {
        public int TiltUp { get => (int)DS4Controls.GyroZNeg; }
        public int TiltDown { get => (int)DS4Controls.GyroZPos; }
        public int TiltLeft { get => (int)DS4Controls.GyroXPos; }
        public int TiltRight { get => (int)DS4Controls.GyroXNeg; }

        public int SwipeUp { get => (int)DS4Controls.SwipeUp; }
        public int SwipeDown { get => (int)DS4Controls.SwipeDown; }
        public int SwipeLeft { get => (int)DS4Controls.SwipeLeft; }
        public int SwipeRight { get => (int)DS4Controls.SwipeRight; }
        public int LSOuterBind { get => (int)DS4Controls.LSOuter; }
        public int RSOuterBind { get => (int)DS4Controls.RSOuter; }

        public int GyroSwipeLeft { get => (int)DS4Controls.GyroSwipeLeft; }
        public int GyroSwipeRight { get => (int)DS4Controls.GyroSwipeRight; }
        public int GyroSwipeUp { get => (int)DS4Controls.GyroSwipeUp; }
        public int GyroSwipeDown { get => (int)DS4Controls.GyroSwipeDown; }
    }
}
