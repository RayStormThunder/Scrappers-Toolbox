using GL_EditorFramework.EditorDrawables;
using GL_EditorFramework.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Toolbox.Library
{
    //Thanks to Smash Forge for a few of these!
    // https://github.com/jam1garner/Smash-Forge/blob/master/Smash%20Forge/Runtime.cs

    public class Runtime
    {
        //Disable loading 3k and higher texture res to prevent slowdown and memory issues
        public static bool DisableLoadingGLHighResTextures = true;

        public static bool EnableDragDrop = true;

        public static bool DumpShadersDEBUG = false;

        public static bool UseSingleInstance = false;
        public static bool UseDirectXTexDecoder = true;
        public static bool DEVELOPER_DEBUG_MODE = false;
        public static bool AlwaysCompressOnSave = false;
        public static bool AlwaysSaveAll = true;

        private const string BackupStateFileName = "backup_state.txt";

        private static string GetBackupRootDirectory(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath))
                return string.Empty;

            string directory = System.IO.Path.GetDirectoryName(sourceFilePath);
            if (string.IsNullOrEmpty(directory))
                directory = ExecutableDir;

            string fileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(sourceFilePath);
            return System.IO.Path.Combine(directory, fileNameNoExt + " - Backup");
        }

        private static string GetBackupStateFilePath(string sourceFilePath)
        {
            string backupRootDir = GetBackupRootDirectory(sourceFilePath);
            if (string.IsNullOrEmpty(backupRootDir))
                return string.Empty;

            return System.IO.Path.Combine(backupRootDir, BackupStateFileName);
        }

        public static bool IsBackupEnabledForFile(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath))
                return false;

            string backupRootDir = GetBackupRootDirectory(sourceFilePath);
            if (!Directory.Exists(backupRootDir))
                return false;

            string statePath = GetBackupStateFilePath(sourceFilePath);
            if (!File.Exists(statePath))
                return false;

            try
            {
                string stateText = File.ReadAllText(statePath).Trim();
                bool enabled;
                if (bool.TryParse(stateText, out enabled))
                    return enabled;
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static void SetBackupEnabledForFile(string sourceFilePath, bool enabled)
        {
            if (string.IsNullOrEmpty(sourceFilePath))
                return;

            string backupRootDir = GetBackupRootDirectory(sourceFilePath);
            if (string.IsNullOrEmpty(backupRootDir))
                return;

            if (enabled)
            {
                Directory.CreateDirectory(backupRootDir);
            }
            else if (!Directory.Exists(backupRootDir))
            {
                // Do not create backup folder/state file when disabling and no folder exists.
                return;
            }

            string statePath = GetBackupStateFilePath(sourceFilePath);
            File.WriteAllText(statePath, enabled ? "true" : "false");
        }

        public static string CreateBackupFilePath(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath))
                return sourceFilePath;

            string fileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(sourceFilePath);
            string extension = System.IO.Path.GetExtension(sourceFilePath);

            string backupRootDir = GetBackupRootDirectory(sourceFilePath);
            Directory.CreateDirectory(backupRootDir);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string baseFileName = fileNameNoExt + "_" + timestamp;
            string candidate = System.IO.Path.Combine(backupRootDir, baseFileName + extension);

            int attempt = 1;
            while (File.Exists(candidate))
            {
                candidate = System.IO.Path.Combine(backupRootDir, baseFileName + "_" + attempt.ToString("D2") + extension);
                attempt++;
            }

            return candidate;
        }

        public static void BackupOriginalOnLoad(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                return;

            if (!IsBackupEnabledForFile(sourceFilePath))
                return;

            if (HasBytePerfectBackup(sourceFilePath))
                return;

            string backupPath = CreateBackupFilePath(sourceFilePath);
            File.Copy(sourceFilePath, backupPath);
        }

        public static void BackupSavedFile(string savedFilePath)
        {
            if (string.IsNullOrEmpty(savedFilePath) || !File.Exists(savedFilePath))
                return;

            if (!IsBackupEnabledForFile(savedFilePath))
                return;

            string backupPath = CreateBackupFilePath(savedFilePath);
            File.Copy(savedFilePath, backupPath);
        }

        private static bool HasBytePerfectBackup(string sourceFilePath)
        {
            string extension = System.IO.Path.GetExtension(sourceFilePath);
            string backupRootDir = GetBackupRootDirectory(sourceFilePath);

            if (!Directory.Exists(backupRootDir))
                return false;

            foreach (string candidate in Directory.GetFiles(backupRootDir, "*" + extension, SearchOption.AllDirectories))
            {
                if (FilesAreByteEqual(sourceFilePath, candidate))
                    return true;
            }

            return false;
        }

        private static bool FilesAreByteEqual(string firstPath, string secondPath)
        {
            var firstInfo = new FileInfo(firstPath);
            var secondInfo = new FileInfo(secondPath);
            if (firstInfo.Length != secondInfo.Length)
                return false;

            const int bufferSize = 81920;
            var bufferA = new byte[bufferSize];
            var bufferB = new byte[bufferSize];

            using (var streamA = File.OpenRead(firstPath))
            using (var streamB = File.OpenRead(secondPath))
            {
                while (true)
                {
                    int readA = streamA.Read(bufferA, 0, bufferA.Length);
                    int readB = streamB.Read(bufferB, 0, bufferB.Length);

                    if (readA != readB)
                        return false;

                    if (readA == 0)
                        return true;

                    for (int i = 0; i < readA; i++)
                    {
                        if (bufferA[i] != bufferB[i])
                            return false;
                    }
                }
            }
        }

        public static class ResourceTables
        {
            public static bool TpTable = false;
            public static bool BotwTable = false;
        }

        public static string PkSwShGamePath = "";
        public static string Mk8GamePath = "";
        public static string Mk8dGamePath = "";
        public static string SmoGamePath = "";
        public static string TpGamePath = "";
        public static string BotwGamePath = "";
        public static string TotkGamePath = "";

        public static bool ShowCloseDialog = true;

        public class UVEditor
        {
            public static Color UVColor = Color.FromArgb(255, 128, 0);
        }

        public class SwitchKeys
        {
            public static string SwitchFolder = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile), ".switch");

            public static string TitleKeys = System.IO.Path.Combine(SwitchFolder, "title.keys");
            public static string ProdKeys = System.IO.Path.Combine(SwitchFolder, "prod.keys");

            public static bool HasKeys()
            {
                Console.WriteLine($"ProdKeys {ProdKeys} Exists? {System.IO.File.Exists(ProdKeys)}");
                Console.WriteLine($"TitleKeys {TitleKeys} Exists? {System.IO.File.Exists(TitleKeys)}");

                return System.IO.File.Exists(ProdKeys) &&
                       System.IO.File.Exists(TitleKeys);
            }
        }

        public class CollisionSettings
        {
            public static string KCLGamePreset = "Default";
            public static string KCLPlatform = "SWITCH";
            public static bool KCLUsePresetEditor = false;
        }

        public class NUTEXBSettings
        {
            public static bool LimitFileSize = false;
            public static bool PadFileSize = false;
        }

        public class MessageEditor
        {
            public static FontFamily FontFamily = new FontFamily("Arial");

            public static int FontSize = 12;
        }

        public class ByamlEditor {
            public static ByamlTextFormat TextFormat = ByamlTextFormat.YAML;
        }

        public enum ByamlTextFormat
        {
            XML_EditorCore,
            XML_YamlConv,
            YAML,
        }

        public class MuuntEditor
        {
            public static bool Enable3DViewport = false;
        }

        public class LayoutEditor
        {
            public static bool AnimationEditMode = false;
            public static bool ForceMaxWidthTimeline = false;

            public static bool TransformChidlren = false;

            public static bool PartsAsNullPanes = false;
            public static bool IsGamePreview = false;
            public static bool DisplayNullPane = true;
            public static bool DisplayTextPane = true;
            public static bool DisplayBoundryPane = true;
            public static bool DisplayPicturePane = true;
            public static bool DisplayWindowPane = true;
            public static bool DisplayAlignmentPane = true;
            public static bool DisplayScissorPane = true;

            //Index for which tab to choose when selected
            //Defaults to last tab used
            public static int PicturePaneTabIndex = 0;
            public static int NullPaneTabIndex = 0;
            public static int WindowPaneTabIndex = 0;
            public static int TextPaneTabIndex = 0;
            public static int MaterialTabIndex = 0;

            public static bool DisplayGrid = true;
            public static bool UseOrthographicView = true;

            public static Color BackgroundColor = Color.FromArgb(130, 130, 130);

            public static DebugShading Shading = DebugShading.Default;
            public static TextureVisibilityMode VisibilityMode = TextureVisibilityMode.FollowsAnimation;
            public static MulticlickBehaviorMode MulticlickBehavior = MulticlickBehaviorMode.Absolute;
            public static bool TransformDataToAnimationData = false;
            public static bool AnimationWindowBoundsInitialized = false;
            public static Point AnimationWindowLocation = new Point(60, 60);
            public static Size AnimationWindowSize = new Size(907, 638);

            public enum DebugShading
            {
                Default,
                VertexColor,
                WhiteColor,
                BlackColor,
                UVTestPattern,
            }

            public enum TextureVisibilityMode
            {
                AlwaysVisible,
                PartialTransparency,
                FollowsAnimation,
                NeverVisible,
            }

            public enum MulticlickBehaviorMode
            {
                Absolute,
                Relative,
            }
        }

        public class ImageEditor
        {
            public static bool PreviewGammaFix = false;

            public static bool ShowPropertiesPanel = true;
            public static bool DisplayVertical = false;


            public static bool DisplayAlpha = true;
            public static bool UseComponetSelector = true;

            public static bool EnableImageZoom = true;
            public static bool EnablePixelGrid = false;
        }

        public class ObjectEditor
        {
            public static bool OpenModelsOnOpen = false;

            public static Point Location = new Point(364, 0);

            public static int EditorDiplayIndex = 0;

            public static int ListPanelWidth;
        }

        //Used for experimental edits
        public static bool useEditDebugMode = false;
        public static bool UseEditDebugMode
        {
            get
            {
                if (useEditDebugMode == false)
                {
                  var result =  MessageBox.Show("This feature is experimental and could lead to potential crashes. Are you sure you want to continue?",
                        "", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, MessageBoxOptions.DefaultDesktopOnly);

                    if (result == DialogResult.Yes)
                        useEditDebugMode = true;
                }
                return useEditDebugMode;
            }
        }

        public static int SelectedBoneIndex = -1;

        //These are so we can load multiple drawables in the viewport
        //Each viewport can carry it's own set of drawables
        //When a viewport is made it will add a ViewportEditor instance and add drawables
        //These will be switchable in settings to load specic objects in the scene or display all of them at once
        public static List<ViewportEditor> viewportEditors = new List<ViewportEditor>();
        public class ViewportEditor
        {
            public List<EditableObject> editableDrawables = new List<EditableObject>();
            public List<AbstractGlDrawable> staticDrawables = new List<AbstractGlDrawable>();

            public static int Width = 638;
        }

        public static TEX_FORMAT PreferredTexFormat = TEX_FORMAT.BC1_UNORM_SRGB;

        public static string ExecutableDir;

        public static Form MainForm;

        public static bool UseDebugDomainExceptionHandler;
        public static bool DisableUpdatePrompt;

        public static bool MaximizeMdiWindow = false;

        public static bool AddFilesToActiveObjectEditor = true;

        public class PBR
        {
            public static bool UseSkybox = false;
            public static bool UseDiffuseSkyTexture = true;
            
            public static string DiffuseCubeMapPath = "";
            public static string SpecularCubeMapPath = "";
            public static string BRSFMapPath = "";
        }

        public static GridSettings gridSettings = new GridSettings();
        public class GridSettings
        {
            public float CellSize = 1.0f;
            public uint CellAmount = 10;
            public Color color = Color.FromArgb(90, 90, 90);
        }

        public static float MaxCameraSpeed = 0.1f;

        public static CameraMode ViewportCameraMode = CameraMode.Perspective;
        public enum CameraMode
        {
            Perspective,
            Orthographic,
        }

        public static int Yaz0CompressionLevel = 3;
        public static bool RenderModels = true;
        public static bool RenderModelSelection = true;
        public static bool RenderModelWireframe = false;
        public static ViewportShading viewportShading;
        public static bool IsDebugMode = true; //Enables experimental features and other things to debug. 
        public static bool EnableVersionCheck = true;
        public static bool EnablePBR = true;

        public static Color CustomPicureBoxBGColor = Color.DarkCyan;

        public static bool enableVSync = false;
        public static float floorSize = 30f;
        public static Color floorColor = Color.Gray;
        public static FloorStyle floorStyle = FloorStyle.WireFrame;
        public static PictureBoxBG pictureBoxStyle = PictureBoxBG.Checkerboard;
        public static float previewScale = 1.0f;

        public static bool renderFloorLines = true;

        public static bool UseOpenGL = true; //Removes all acess to opengl functionality. Useful for old PCs
        public static bool DisplayViewport = true; //Only displays it in editors if true

        //Viewport Background
        public static BackgroundStyle backgroundStyle = BackgroundStyle.Gradient;
        public static bool renderBackGround = true;
        public static string backgroundTexFilePath = "";
        public static Color backgroundGradientTop = Color.FromArgb(255, 26, 26, 26);
        public static Color backgroundGradientBottom = Color.FromArgb(255, 77, 77, 77);
        public static float zoomspeed = 1.25f;
        public static float zoomModifierScale = 2.0f;
        public static bool cameraLight = false;
        public static bool DisplayPolyCount = true;
        public static float PolyCount = 0;
        public static float VertCount = 0;

        public static bool enableOpenTKDebugOutput = false;

        public static bool OpenStartupWindow = false;

        // Toggle Render Passes
        public static bool renderDiffuse = true;
        public static bool renderFresnel = true;
        public static bool renderSpecular = true;
        public static bool renderReflection = true;
        public static bool renderBoundingBoxes = false;
        public static bool renderNormalMap = true;
        public static bool renderVertColor = true;
        public static bool renderBfresPbr = false;
        public static bool renderNormalsPoints = false;
        public static bool renderBones = true;
        public static bool renderFog = true;

        public static bool displayAxisLines = true;
        public static bool displayGrid = true;

        public static bool FrameCamera = false;

        public static float bonePointSize = 0.2f;

        public static bool boneXrayDisplay = false;

        public static float normalsLineLength = 1;

        public static bool stereoscopy = false;
        public static bool UseLegacyGL = false;
        public static bool OpenTKInitialized = false;

        public static bool useNormalMap = true;

        public static CameraMovement cameraMovement;
        public static CameraView cameraView;

        public static ThumbnailSize thumbnailSize = ThumbnailSize.Small;

        public static float CameraNear = 0.1f;
        public static float CameraFar = 100000.0f;
        public static ActiveGame activeGame = ActiveGame.SMO;

        public static string ProgramVersion = "0.0.0";
        public static string CompileDate = "0/0/0000";
        public static string CommitInfo = "";

        public enum ActiveGame
        {
            SMO,
            MK8D,
            ARMs,
            Splatoon2,
            BOTW,
            KSA,
            Bezel,
        }

        public enum PictureBoxBG
        {
            Checkerboard,
            Black,
            White,
            Custom,
        }

        public enum CameraMovement
        {
            Inspect,
            Walk,
        }
        public enum CameraView
        {
            Perspective,
            Orthographic,
        }

        public enum ThumbnailSize
        {
            Small,
            Medium,
            Large
        }

        public enum BackgroundStyle
        {
            Gradient = 0,
            UserTexture = 1,
            Solid = 2,
        }

        public enum FloorStyle
        {
            WireFrame = 0,
            UserTexture = 1,
            Solid = 2,
        }

        public enum ViewportShading
        {
            Default = 0,
            Normal = 1,
            Lighting = 2,
            Diffuse = 3,
            NormalMap = 4,
            VertColor = 5,
            AmbientOcclusion = 6,
            UVCoords = 7,
            UVTestPattern = 8,
            Tangents = 9,
            Bitangents = 10,
            LightMap = 11,
            SelectedBoneWeights = 12,
            SpecularMap = 13,
            ShadowMap = 14,
            MetalnessMap = 15,
            RoughnessMap = 16,
            SubSurfaceScatteringMap = 17,
            EmmissionMap = 18,
        }
        public enum UVChannel
        {
            Channel1 = 1,
            Channel2 = 2,
            Channel3 = 3
        }

        // Debug Shading
        public static bool renderR = true;
        public static bool renderG = true;
        public static bool renderB = true;
        public static bool renderAlpha = true;
        public static UVChannel uvChannel = UVChannel.Channel1;


        // OpenGL System Information
        public static string renderer = "";
        public static string openGLVersion = "";
        public static string GLSLVersion = "";
    }
}
