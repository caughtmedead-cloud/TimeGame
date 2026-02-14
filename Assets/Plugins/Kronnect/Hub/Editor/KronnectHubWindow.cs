using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using HubAssetStoreUtility = Kronnect.Hub.AssetStoreUtility;

namespace Kronnect.Hub {

    internal sealed class KronnectHubWindow : EditorWindow {

        const string WindowTitle = "Kronnect Hub";
        const string ShowOnlyErrorsPref = "KronnectHub.ShowOnlyErrors";
        const string GlobalValidationsId = "__global__";
        const float ProductColumnWidth = 265f;
        const float CheckColumnWidth = 120f;
        const float LinksColumnWidth = 130f;
        const float LinksButtonWidth = LinksColumnWidth;
        const float IconSpacing = 6f;
        const float ColumnSpacing = 12f;
        const float WindowFixedWidth = 800f;
        const float WindowMinHeight = 400f;
        const float WindowMaxHeight = 4000f;
        const float SplitterHeight = 6f;
        const float ResultsMinHeight = 100f;
        const float ResultsMaxHeight = 600f;
        const float ResultsDefaultHeight = 200f;
        static readonly GUIContent ResultsLabel = new GUIContent("Results");
        static readonly GUIContent InfoLabel = new GUIContent("Setup Guidance");
        static readonly Color SuccessColor = new Color(0.5f, 0.9f, 0.5f);
        static readonly Color WarningColor = new Color(1f, 0.92f, 0.2f);
        static readonly GUIContent SupportForumButtonContent = new GUIContent("Support Forum", "Open Kronnect support forum in your browser.");
        static readonly GUIContent DiscordButtonContent = new GUIContent("Discord", "Join the Kronnect Discord server.");
        static readonly GUIContent VerifyAllButtonContent = new GUIContent("Verify All Assets ▶", "Run setup checks for every listed asset.");
        static readonly GUIContent RefreshButtonContent = new GUIContent("Refresh", "Refresh cached package availability.");
        static readonly GUIContent CheckSetupButtonContent = new GUIContent("Check Setup", "Validate render features and required settings for this asset.");
        static readonly GUIContent FindInSceneButtonContent = new GUIContent("Find in Scene", "Locate this asset’s components or volumes in the current scene.");
        static readonly GUIContent ImportButtonContent = new GUIContent("Package Manager", "Open the Unity Package Manager to install this asset.");
        static readonly GUIContent AssetStoreButtonContent = new GUIContent("Asset Store", "Open this asset’s page in the Asset Store.");
        static readonly GUIContent OpenFolderButtonContent = new GUIContent("Open Folder", "Open the installed asset folder in the Project window.");
        static readonly GUIContent CleanUpButtonContent = new GUIContent("Clean Up & Remove", "Remove the asset and clean related project entries.");
        static readonly GUIContent ShowPackageButtonContent = new GUIContent("Show Package", "Reveal the cached unitypackage location in the OS file browser.");
        static readonly GUIContent ShowInAssetStoreButtonContent = new GUIContent("Show in Asset Store", "Open this asset’s page in the Asset Store.");

        readonly Dictionary<string, ValidationReport> _validationReports = new Dictionary<string, ValidationReport>();
        readonly Dictionary<string, Texture2D> _assetIcons = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, bool> _cachedPackageAvailability = new Dictionary<string, bool>();
        readonly Dictionary<string, string> _cachedPackagePaths = new Dictionary<string, string>();
        readonly HashSet<string> _assetsNeedingSetupCheck = new HashSet<string>();
        readonly GlobalValidationProvider _globalValidationProvider = new GlobalValidationProvider();
        Texture2D _logo;
        GUIStyle _statusBadgeStyle;
        GUIStyle _validationLabelStyle;
        GUIStyle _validationStatusStyle;
        GUIStyle _tableHeaderLeftStyle;
        GUIStyle _tableHeaderCenterStyle;
        GUIStyle _verifyButtonStyle;
        bool _resultsExpanded;
        float _resultsHeight = ResultsDefaultHeight;
        bool _isDraggingSplitter;
        bool _showOnlyErrors;
        Vector2 _gridScroll;
        Vector2 _resultsScroll;
        string _activeConsoleAssetId;
        bool _isFindMode;
        bool _isSingleAssetMode;

        [MenuItem("Window/Kronnect/Hub")]
        static void ShowWindow() {
            GetWindow<KronnectHubWindow>(false, WindowTitle, true);
        }

        void OnEnable() {
            titleContent = new GUIContent(WindowTitle);
            _resultsExpanded = false;
            _showOnlyErrors = EditorPrefs.GetBool(ShowOnlyErrorsPref, false);
            UpdateWindowSize();
            _logo = Resources.Load<Texture2D>("kronnectLogo");
            _validationReports[GlobalValidationsId] = new ValidationReport();
            foreach (AssetIntegration asset in AssetRegistry.Assets) {
                if (!_validationReports.ContainsKey(asset.Id)) {
                    _validationReports[asset.Id] = new ValidationReport();
                }
            }
            _activeConsoleAssetId = AssetRegistry.Assets.First().Id;
            LoadAssetIcons();
            RefreshCachedPackageAvailability();
            VerifyAllSetups();
            
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        void RefreshCachedPackageAvailability() {
            _cachedPackageAvailability.Clear();
            _cachedPackagePaths.Clear();
            foreach (AssetIntegration asset in AssetRegistry.Assets) {
                string searchName = asset.CacheSearchName ?? asset.DisplayName;
                string packagePath = HubAssetStoreUtility.FindCachedPackage(asset.PublisherName, searchName);
                bool hasCachedPackage = !string.IsNullOrEmpty(packagePath) && File.Exists(packagePath);
                _cachedPackageAvailability[asset.Id] = hasCachedPackage;
                if (hasCachedPackage) {
                    _cachedPackagePaths[asset.Id] = packagePath;
                }
            }
        }

        void OnDisable() {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            DisposeAssetIcons();
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode) {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                VerifyAllSetups();
            }
        }

        void OnSceneClosed(Scene scene) {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                VerifyAllSetups();
            }
        }

        void OnGUI() {
            CreateStyles();
            DrawHeader();
            GUILayout.Space(8f);
            DrawMatrix();
            DrawSplitter();
            DrawResultsArea();
        }

        void DrawHeader() {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    if (_logo != null) {
                        Rect logoRect = GUILayoutUtility.GetRect(120f, 40f, GUILayout.ExpandWidth(false));
                        if (GUI.Button(logoRect, GUIContent.none, GUIStyle.none)) {
                            Application.OpenURL("https://kronnect.com");
                        }
                        GUI.DrawTexture(logoRect, _logo, ScaleMode.ScaleToFit);
                        EditorGUIUtility.AddCursorRect(logoRect, MouseCursor.Link);
                    }
                    GUILayout.FlexibleSpace();
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(140f))) {
                        if (GUILayout.Button(SupportForumButtonContent)) {
                            Application.OpenURL("https://kronnect.com/support");
                        }
                        if (GUILayout.Button(DiscordButtonContent)) {
                            Application.OpenURL("https://discord.gg/EH2GMaM");
                        }
                    }
                }
                EditorGUILayout.LabelField(InfoLabel, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("• Check Setup verifies that the active URP asset includes all required render features and settings.\n• Find locates all volumes/components with the asset in the scene. Utility links provide quick access to asset documentation and demo scenes and allows removing the asset from the project.", EditorStyles.wordWrappedLabel);
            }
        }

        void DrawMatrix() {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true))) {
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(VerifyAllButtonContent, _verifyButtonStyle, GUILayout.Width(145f), GUILayout.Height(28f))) {
                        VerifyAllSetups();
                    }
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(2f);
                DrawTableHeader();
                _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
                foreach (AssetIntegration asset in AssetRegistry.Assets.OrderByDescending(a => AssetRuntimeState.Get(a).IsInstalled)) {
                    DrawAssetRow(asset);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawTableHeader() {
            const float helpBoxPadding = 6f;
            const float refreshButtonWidth = 55f;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.Space(helpBoxPadding);
                GUILayout.Label("Asset", _tableHeaderLeftStyle, GUILayout.Width(ProductColumnWidth - refreshButtonWidth));
                if (GUILayout.Button(RefreshButtonContent, EditorStyles.toolbarButton, GUILayout.Width(refreshButtonWidth))) {
                    RefreshCachedPackageAvailability();
                }
                GUILayout.Space(ColumnSpacing);
                GUILayout.Label("Actions", _tableHeaderCenterStyle, GUILayout.Width(CheckColumnWidth));
                GUILayout.Space(ColumnSpacing);
                GUILayout.Space(12);
                GUILayout.Label("Utility", _tableHeaderLeftStyle, GUILayout.ExpandWidth(true));
                GUILayout.Space(60);
            }
        }

        void DrawAssetRow(AssetIntegration asset) {
            AssetRuntimeState state = AssetRuntimeState.Get(asset);
            bool isInstalled = state.IsInstalled;
            bool hasCachedPackage = false;
            string cachedPackagePath = null;

            if (!isInstalled) {
                hasCachedPackage = _cachedPackageAvailability.TryGetValue(asset.Id, out bool cached) && cached;
                if (hasCachedPackage) {
                    _cachedPackagePaths.TryGetValue(asset.Id, out cachedPackagePath);
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ProductColumnWidth))) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        Texture2D icon = GetAssetIcon(asset.Id);
                        if (icon != null) {
                            float iconSize = Mathf.Ceil(EditorGUIUtility.singleLineHeight * 2.5f);
                            GUILayout.Label(icon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
                            GUILayout.Space(IconSpacing);
                        }
                        using (new EditorGUILayout.VerticalScope()) {
                            EditorGUILayout.LabelField(asset.DisplayName, EditorStyles.boldLabel);
                            if (isInstalled) {
                                bool needsCheck = _assetsNeedingSetupCheck.Contains(asset.Id);
                                DrawStatusBadge(needsCheck ? "In Project - Setup Needs Check" : "In Project", needsCheck);
                            } else {
                                EditorGUILayout.LabelField(asset.Description ?? "", EditorStyles.miniLabel);
                            }
                        }
                    }
                }

                GUILayout.Space(ColumnSpacing);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(CheckColumnWidth))) {
                    if (isInstalled) {
                        if (GUILayout.Button(CheckSetupButtonContent, GUILayout.Width(CheckColumnWidth))) {
                            RunValidation(asset);
                        }
                        if ((asset.VolumeComponentType != null || asset.SceneComponentType != null) && GUILayout.Button(FindInSceneButtonContent, GUILayout.Width(CheckColumnWidth))) {
                            if (asset.VolumeComponentType != null) {
                                FindVolumesWithComponent(asset);
                            } else {
                                FindSceneComponents(asset);
                            }
                        }
                    } else {
                        if (hasCachedPackage) {
                            if (GUILayout.Button(ImportButtonContent, GUILayout.Width(CheckColumnWidth))) {
                                string packageId = string.IsNullOrEmpty(asset.AssetStoreId) ? null : asset.AssetStoreId;
                                UnityEditor.PackageManager.UI.Window.Open(packageId);
                            }
                        } else {
                            if (GUILayout.Button(AssetStoreButtonContent, GUILayout.Width(CheckColumnWidth))) {
                                HubAssetStoreUtility.OpenAssetStoreUrl(asset.AssetStoreUrl);
                            }
                        }
                    }
                }

                GUILayout.Space(ColumnSpacing);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(LinksColumnWidth))) {
                    if (isInstalled) {
                        string folderToOpen = state.InstalledFolder ?? GetInstalledFolder(asset);
                        using (new EditorGUI.DisabledScope(!HasValidFolder(folderToOpen))) {
                            if (GUILayout.Button(OpenFolderButtonContent, GUILayout.Width(LinksButtonWidth))) {
                                SelectFolderInProject(folderToOpen);
                            }
                        }
                        if (GUILayout.Button(CleanUpButtonContent, GUILayout.Width(LinksButtonWidth))) {
                            RemoveAssetFromProject(asset);
                        }
                        if (!string.IsNullOrEmpty(asset.AssetStoreUrl) && GUILayout.Button(ShowInAssetStoreButtonContent, GUILayout.Width(LinksButtonWidth))) {
                            HubAssetStoreUtility.OpenAssetStoreUrl(asset.AssetStoreUrl);
                        }
                    } else if (hasCachedPackage) {
                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(cachedPackagePath))) {
                            if (GUILayout.Button(ShowPackageButtonContent, GUILayout.Width(LinksButtonWidth))) {
                                HubAssetStoreUtility.RevealPackageLocation(cachedPackagePath);
                            }
                        }
                        if (!string.IsNullOrEmpty(asset.AssetStoreUrl) && GUILayout.Button(ShowInAssetStoreButtonContent, GUILayout.Width(LinksButtonWidth))) {
                            HubAssetStoreUtility.OpenAssetStoreUrl(asset.AssetStoreUrl);
                        }
                    }
                }
            }
        }

        void DrawSplitter() {
            bool hasReports = _validationReports.Values.Any(r => r != null && r.HasEntries);
            if (!hasReports || !_resultsExpanded) return;

            Rect splitterRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(SplitterHeight), GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            Color splitterColor = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.6f, 0.6f, 0.6f);
            EditorGUI.DrawRect(splitterRect, splitterColor);

            Rect gripRect = new Rect(splitterRect.center.x - 20f, splitterRect.y + 2f, 40f, 2f);
            Color gripColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);
            EditorGUI.DrawRect(gripRect, gripColor);

            Event evt = Event.current;
            switch (evt.type) {
                case EventType.MouseDown:
                    if (splitterRect.Contains(evt.mousePosition)) {
                        _isDraggingSplitter = true;
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (_isDraggingSplitter) {
                        _resultsHeight -= evt.delta.y;
                        _resultsHeight = Mathf.Clamp(_resultsHeight, ResultsMinHeight, ResultsMaxHeight);
                        Repaint();
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (_isDraggingSplitter) {
                        _isDraggingSplitter = false;
                        evt.Use();
                    }
                    break;
            }
        }

        void DrawResultsArea() {
            bool hasReports = _validationReports.Values.Any(r => r != null && r.HasEntries);
            bool newExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_resultsExpanded, ResultsLabel);
            if (newExpanded != _resultsExpanded) {
                SetResultsExpanded(newExpanded);
            }
            if (_resultsExpanded) {
                using (new EditorGUILayout.VerticalScope(GUILayout.Height(_resultsHeight))) {
                    if (!hasReports) {
                        EditorGUILayout.HelpBox("No validation results available.", MessageType.Info);
                    } else if (_isFindMode && !string.IsNullOrEmpty(_activeConsoleAssetId)) {
                        ValidationReport findReport = _validationReports.TryGetValue(_activeConsoleAssetId, out var existing) ? existing : null;
                        if (findReport != null && findReport.HasEntries) {
                            DrawFindResults(findReport);
                        }
                    } else {
                        DrawAllValidationResults();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawAllValidationResults() {
            // Check for global validations
            bool hasGlobalReport = _validationReports.TryGetValue(GlobalValidationsId, out ValidationReport globalReport) && 
                                   globalReport != null && globalReport.HasEntries;

            List<(AssetIntegration asset, ValidationReport report)> assetsWithReports = new List<(AssetIntegration, ValidationReport)>();
            foreach (AssetIntegration asset in AssetRegistry.Assets) {
                if (_validationReports.TryGetValue(asset.Id, out ValidationReport report) && report != null && report.HasEntries) {
                    assetsWithReports.Add((asset, report));
                }
            }

            if (!hasGlobalReport && assetsWithReports.Count == 0) {
                EditorGUILayout.HelpBox("No validation results available.", MessageType.Info);
                return;
            }

            bool hasAnyInvalidWithFix = assetsWithReports.Any(x => x.report.HasInvalidWithFix) ||
                                        (hasGlobalReport && globalReport.HasInvalidWithFix);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUI.BeginChangeCheck();
                _showOnlyErrors = GUILayout.Toggle(_showOnlyErrors, "Only Show Errors", GUILayout.Width(120f));
                if (EditorGUI.EndChangeCheck()) {
                    EditorPrefs.SetBool(ShowOnlyErrorsPref, _showOnlyErrors);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear All", GUILayout.Width(70f))) {
                    ClearAllReports();
                }
                using (new EditorGUI.DisabledScope(!hasAnyInvalidWithFix)) {
                    if (GUILayout.Button("Fix All", GUILayout.Width(60f))) {
                        ExecuteFixAllAssets();
                    }
                }
                GUILayout.Space(10f);
            }

            _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll);
            bool firstSectionDrawn = false;

            // Draw global validations first
            if (hasGlobalReport) {
                if (!_showOnlyErrors || globalReport.HasInvalid) {
                    DrawGlobalValidationResults(globalReport);
                    firstSectionDrawn = true;
                }
            }

            // Draw asset-specific validations
            for (int assetIndex = 0; assetIndex < assetsWithReports.Count; assetIndex++) {
                var (asset, report) = assetsWithReports[assetIndex];
                if (_showOnlyErrors && !report.HasInvalid) continue;
                if (firstSectionDrawn) {
                    GUILayout.Space(8f);
                }
                DrawValidationResultsForAsset(asset, report);
                firstSectionDrawn = true;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
        }

        void ClearAllReports() {
            foreach (var key in _validationReports.Keys.ToList()) {
                _validationReports[key].Clear();
            }
            SetResultsExpanded(false);
        }

        void ExecuteFixAllAssets() {
            List<ValidationEntry> allEntriesToFix = new List<ValidationEntry>();

            // Include global validation fixes first
            if (_validationReports.TryGetValue(GlobalValidationsId, out ValidationReport globalReport) && globalReport != null) {
                allEntriesToFix.AddRange(globalReport.Entries.Where(e => !e.IsOptionalAction && !e.IsValid && e.FixAction != null));
            }

            // Include asset-specific validation fixes
            foreach (AssetIntegration asset in AssetRegistry.Assets) {
                if (_validationReports.TryGetValue(asset.Id, out ValidationReport report) && report != null) {
                    allEntriesToFix.AddRange(report.Entries.Where(e => !e.IsOptionalAction && !e.IsValid && e.FixAction != null));
                }
            }

            int fixesApplied = 0;
            foreach (ValidationEntry entry in allEntriesToFix) {
                if (!entry.NeedsFix) continue;
                try {
                    entry.FixAction.Invoke();
                    fixesApplied++;
                } catch (Exception ex) {
                    Debug.LogError($"Fix for '{entry.Description}' failed: {ex.Message}");
                }
            }

            if (fixesApplied > 0) {
                VerifyAllSetups();
            }
        }

        void SetResultsExpanded(bool expanded) {
            if (_resultsExpanded == expanded) return;
            _resultsExpanded = expanded;
            Repaint();
        }

        void UpdateWindowSize() {
            minSize = new Vector2(WindowFixedWidth, WindowMinHeight);
            maxSize = new Vector2(WindowFixedWidth, WindowMaxHeight);
        }

        void DrawValidationResultsForAsset(AssetIntegration asset, ValidationReport report) {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.Space(4f);
                GUILayout.Label($"Validation of {asset.DisplayName}: ", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                Color prevColor = GUI.color;
                if (report.HasInvalid) {
                    GUI.color = WarningColor;
                    GUILayout.Label("NEED REVIEW", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                } else {
                    GUI.color = SuccessColor;
                    GUILayout.Label("PASSED", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                }
                GUI.color = prevColor;
                GUILayout.Label("Status", GUILayout.Width(55f));
                GUILayout.Label("Actions", GUILayout.Width(180f));
            }

            IReadOnlyList<ValidationEntry> sortedEntries = report.SortedEntries;
            int displayIndex = 0;
            for (int i = 0; i < sortedEntries.Count; i++) {
                ValidationEntry entry = sortedEntries[i];
                if (_showOnlyErrors && (entry.IsOptionalAction || entry.IsValid)) continue;
                displayIndex++;
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(26f))) {
                    string numberedDescription = $"{displayIndex}. {entry.Description}";
                    GUIContent labelContent = string.IsNullOrEmpty(entry.Details)
                        ? new GUIContent(numberedDescription)
                        : new GUIContent(numberedDescription, entry.Details);
                    GUILayout.Label(labelContent, _validationLabelStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                    if (entry.IsOptionalAction) {
                        GUILayout.Label("", _validationStatusStyle, GUILayout.Width(55f), GUILayout.ExpandHeight(true));
                        using (new EditorGUILayout.HorizontalScope(GUILayout.Width(170f))) {
                            string fixLabel = entry.CustomFixLabel ?? "Execute";
                            bool canExecute = entry.FixAction != null;
                            bool useSplitButtons = (entry.CustomFixLabel != null && entry.CustomFixLabel != "Execute") || entry.CanShow;
                            
                            if (useSplitButtons) {
                                if (canExecute) {
                                    if (GUILayout.Button(fixLabel, GUILayout.Width(55f))) {
                                        if (ConfirmOptionalAction(entry)) {
                                            ExecuteFix(asset, entry);
                                        }
                                    }
                                } else {
                                    GUILayout.Space(58f);
                                }
                                using (new EditorGUI.DisabledScope(!entry.CanShow)) {
                                    if (GUILayout.Button("Show", GUILayout.Width(50f)) && entry.CanShow) {
                                        if (entry.ShowAction != null) {
                                            entry.ShowAction.Invoke();
                                        } else if (entry.ShowTarget != null) {
                                            Selection.activeObject = entry.ShowTarget;
                                            EditorGUIUtility.PingObject(entry.ShowTarget);
                                        }
                                    }
                                }
                            } else {
                                using (new EditorGUI.DisabledScope(!canExecute)) {
                                    if (GUILayout.Button(fixLabel, GUILayout.Width(106f)) && canExecute) {
                                        if (ConfirmOptionalAction(entry)) {
                                            ExecuteFix(asset, entry);
                                        }
                                    }
                                }
                            }
                            DrawDetailsButton(entry);
                        }
                    } else {
                        Color prevColor = GUI.color;
                        GUI.color = entry.IsValid ? GUI.color : WarningColor;
                        string statusLabel = entry.IsValid ? "✅" : (entry.IsWarning ? "⚠" : "FAIL");
                        GUILayout.Label(statusLabel, _validationStatusStyle, GUILayout.Width(55f), GUILayout.ExpandHeight(true));
                        GUI.color = prevColor;

                        using (new EditorGUILayout.HorizontalScope(GUILayout.Width(170f))) {
                            string fixLabel = entry.CustomFixLabel ?? "Fix";
                            bool canFix = !entry.IsValid && !entry.IsWarning && entry.FixAction != null;
                            using (new EditorGUI.DisabledScope(!canFix)) {
                                if (GUILayout.Button(fixLabel, GUILayout.Width(55f)) && canFix) {
                                    ExecuteFix(asset, entry);
                                }
                            }
                            using (new EditorGUI.DisabledScope(!entry.CanShow)) {
                                if (GUILayout.Button("Show", GUILayout.Width(50f)) && entry.CanShow) {
                                    if (entry.ShowAction != null) {
                                        entry.ShowAction.Invoke();
                                    } else if (entry.ShowTarget != null) {
                                        Selection.activeObject = entry.ShowTarget;
                                        EditorGUIUtility.PingObject(entry.ShowTarget);
                                    }
                                }
                            }
                            DrawDetailsButton(entry);
                        }
                    }
                }
            }
        }

        void DrawGlobalValidationResults(ValidationReport report) {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.Space(4f);
                GUILayout.Label("General Validations: ", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                Color prevColor = GUI.color;
                if (report.HasInvalid) {
                    GUI.color = WarningColor;
                    GUILayout.Label("NEED REVIEW", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                } else {
                    GUI.color = SuccessColor;
                    GUILayout.Label("PASSED", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                }
                GUI.color = prevColor;
                GUILayout.Label("Status", GUILayout.Width(55f));
                GUILayout.Label("Actions", GUILayout.Width(180f));
            }

            IReadOnlyList<ValidationEntry> sortedEntries = report.SortedEntries;
            int displayIndex = 0;
            for (int i = 0; i < sortedEntries.Count; i++) {
                ValidationEntry entry = sortedEntries[i];
                if (_showOnlyErrors && (entry.IsOptionalAction || entry.IsValid)) continue;
                displayIndex++;
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(26f))) {
                    string numberedDescription = $"{displayIndex}. {entry.Description}";
                    GUIContent labelContent = string.IsNullOrEmpty(entry.Details)
                        ? new GUIContent(numberedDescription)
                        : new GUIContent(numberedDescription, entry.Details);
                    GUILayout.Label(labelContent, _validationLabelStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                    Color statusPrevColor = GUI.color;
                    GUI.color = entry.IsValid ? GUI.color : WarningColor;
                    string statusLabel = entry.IsValid ? "✅" : (entry.IsWarning ? "⚠" : "FAIL");
                    GUILayout.Label(statusLabel, _validationStatusStyle, GUILayout.Width(55f), GUILayout.ExpandHeight(true));
                    GUI.color = statusPrevColor;

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(170f))) {
                        string fixLabel = entry.CustomFixLabel ?? "Fix";
                        bool canFix = !entry.IsValid && !entry.IsWarning && entry.FixAction != null;
                        using (new EditorGUI.DisabledScope(!canFix)) {
                            if (GUILayout.Button(fixLabel, GUILayout.Width(55f)) && canFix) {
                                ExecuteGlobalFix(entry);
                            }
                        }
                        using (new EditorGUI.DisabledScope(!entry.CanShow)) {
                            if (GUILayout.Button("Show", GUILayout.Width(50f)) && entry.CanShow) {
                                if (entry.ShowAction != null) {
                                    entry.ShowAction.Invoke();
                                } else if (entry.ShowTarget != null) {
                                    Selection.activeObject = entry.ShowTarget;
                                    EditorGUIUtility.PingObject(entry.ShowTarget);
                                }
                            }
                        }
                        DrawDetailsButton(entry);
                    }
                }
            }
        }

        void ExecuteGlobalFix(ValidationEntry entry) {
            entry.FixAction?.Invoke();
            _globalValidationProvider.Validate(_validationReports[GlobalValidationsId]);
            VerifyAllSetups();
        }

        void DrawFindResults(ValidationReport report) {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.Space(4f);
                GUILayout.Label($"Volumes found: {report.Entries.Count}", GUILayout.ExpandWidth(true));
                GUILayout.Label("Actions", GUILayout.Width(70f));
            }

            _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll);
            for (int i = 0; i < report.Entries.Count; i++) {
                ValidationEntry entry = report.Entries[i];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(26f))) {
                    string numberedDescription = $"{i + 1}. {entry.Description}";
                    GUILayout.Label(numberedDescription, _validationLabelStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    
                    using (new EditorGUI.DisabledScope(!entry.CanShow)) {
                        if (GUILayout.Button("Select", GUILayout.Width(60f)) && entry.CanShow) {
                            if (entry.ShowTarget != null) {
                                Selection.activeObject = entry.ShowTarget;
                                EditorGUIUtility.PingObject(entry.ShowTarget);
                            }
                        }
                    }
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
        }

        void RunValidation(AssetIntegration asset) {
            _activeConsoleAssetId = asset.Id;
            _isFindMode = false;
            _isSingleAssetMode = true;

            foreach (var key in _validationReports.Keys.ToList()) {
                _validationReports[key].Clear();
            }

            AssetRuntimeState state = AssetRuntimeState.Get(asset);
            state.InvalidateCache();
            
            if (!state.IsInstalled) {
                _validationReports[asset.Id] = new ValidationReport();
                RefreshCachedPackageAvailability();
                Repaint();
                return;
            }

            // Run global validations first
            ValidationReport globalReport = new ValidationReport();
            _validationReports[GlobalValidationsId] = globalReport;
            _globalValidationProvider.Validate(globalReport);

            // Run asset-specific validations
            ValidationReport report = new ValidationReport();
            _validationReports[asset.Id] = report;

            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            UrpUtility.PipelineState pipeline = UrpUtility.GetPipelineState(urpAsset);
            UniversalRendererData rendererData = pipeline?.DefaultRendererData as UniversalRendererData;
            VolumeProfile volumeProfile = VolumeUtility.LoadVolumeProfileAsset();
            Volume setupVolume = VolumeUtility.FindSetupVolume();

            ValidationContext validationContext = new ValidationContext(
                asset,
                report,
                urpAsset,
                rendererData,
                pipeline,
                volumeProfile,
                setupVolume,
                VolumeUtility.VolumeObjectName);

            foreach (IValidationProvider provider in AssetRegistry.GeneralValidationProviders) {
                provider.Validate(validationContext);
            }

            IReadOnlyList<IValidationProvider> assetProviders = null;
            if (AssetRegistry.AssetValidationProviders.TryGetValue(asset.Id, out var providers)) {
                assetProviders = providers;
                foreach (IValidationProvider provider in assetProviders) {
                    provider.Validate(validationContext);
                }
            }

            foreach (IValidationProvider provider in AssetRegistry.GeneralValidationProviders) {
                provider.OnValidationComplete(validationContext);
            }

            if (assetProviders != null) {
                foreach (IValidationProvider provider in assetProviders) {
                    provider.OnValidationComplete(validationContext);
                }
            }

            if (globalReport.HasInvalid || report.HasInvalid) {
                _assetsNeedingSetupCheck.Add(asset.Id);
            } else {
                _assetsNeedingSetupCheck.Remove(asset.Id);
            }

            SetResultsExpanded(true);
        }

        void VerifyAllSetups() {
            _isFindMode = false;
            _isSingleAssetMode = false;
            _assetsNeedingSetupCheck.Clear();
            
            // Run global validations once
            ValidationReport globalReport = new ValidationReport();
            _validationReports[GlobalValidationsId] = globalReport;
            _globalValidationProvider.Validate(globalReport);

            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            UrpUtility.PipelineState pipeline = UrpUtility.GetPipelineState(urpAsset);
            UniversalRendererData rendererData = pipeline?.DefaultRendererData as UniversalRendererData;
            VolumeProfile volumeProfile = VolumeUtility.LoadVolumeProfileAsset();
            Volume setupVolume = VolumeUtility.FindSetupVolume();

            foreach (AssetIntegration asset in AssetRegistry.Assets) {
                AssetRuntimeState state = AssetRuntimeState.Get(asset);
                state.InvalidateCache();
                
                if (!state.IsInstalled) continue;

                ValidationReport report = new ValidationReport();
                _validationReports[asset.Id] = report;

                ValidationContext validationContext = new ValidationContext(
                    asset,
                    report,
                    urpAsset,
                    rendererData,
                    pipeline,
                    volumeProfile,
                    setupVolume,
                    VolumeUtility.VolumeObjectName);

                foreach (IValidationProvider provider in AssetRegistry.GeneralValidationProviders) {
                    provider.Validate(validationContext);
                }

                if (AssetRegistry.AssetValidationProviders.TryGetValue(asset.Id, out var providers)) {
                    foreach (IValidationProvider provider in providers) {
                        provider.Validate(validationContext);
                    }
                }

                if (report.HasInvalid) {
                    _assetsNeedingSetupCheck.Add(asset.Id);
                }
            }

            SetResultsExpanded(true);
            Repaint();
        }

        void FindVolumesWithComponent(AssetIntegration asset) {
            _activeConsoleAssetId = asset.Id;
            _isFindMode = true;

            ValidationReport report = new ValidationReport();
            _validationReports[asset.Id] = report;

            if (string.IsNullOrEmpty(asset.VolumeComponentType)) {
                report.Add("Find Volumes", false, "This asset does not use a Volume component.");
                SetResultsExpanded(true);
                return;
            }

            if (!TypeUtility.TryGetType(asset.VolumeComponentType, out Type componentType)) {
                report.Add("Find Volumes", false, $"Could not find type: {asset.VolumeComponentType}");
                SetResultsExpanded(true);
                return;
            }

            Volume[] allVolumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<Volume> matchingVolumes = new List<Volume>();

            foreach (Volume volume in allVolumes) {
                VolumeProfile profile = volume.sharedProfile;
                if (profile == null) continue;

                bool hasComponent = profile.components.Any(c => c != null && c.GetType() == componentType);
                if (hasComponent) {
                    matchingVolumes.Add(volume);
                }
            }

            if (matchingVolumes.Count == 0) {
                report.Add($"No Volumes found with {componentType.Name}", true, 
                    $"No Volume in the scene contains a {componentType.Name} override. Use 'Check' to set one up automatically.");
            } else {
                foreach (Volume volume in matchingVolumes) {
                    string volumeName = volume.gameObject.name;
                    string sceneName = volume.gameObject.scene.name;
                    bool isGlobal = volume.isGlobal;
                    string globalText = isGlobal ? " (Global)" : " (Local)";

                    report.AddOptionalAction(
                        $"Volume: {volumeName}{globalText} in {sceneName}",
                        $"This Volume contains a {componentType.Name} override. " +
                        (isGlobal ? "It is configured as a global volume, affecting the entire scene." : "It is a local volume with a trigger collider.") +
                        $" Priority: {volume.priority}.",
                        null,
                        volume.gameObject,
                        null,
                        "Select");
                }
            }

            SetResultsExpanded(true);
        }

        void FindSceneComponents(AssetIntegration asset) {
            _activeConsoleAssetId = asset.Id;
            _isFindMode = true;

            ValidationReport report = new ValidationReport();
            _validationReports[asset.Id] = report;

            if (string.IsNullOrEmpty(asset.SceneComponentType)) {
                report.Add("Find Components", false, "This asset does not use a scene component.");
                SetResultsExpanded(true);
                return;
            }

            if (!TypeUtility.TryGetType(asset.SceneComponentType, out Type componentType)) {
                report.Add("Find Components", false, $"Could not find type: {asset.SceneComponentType}");
                SetResultsExpanded(true);
                return;
            }

            UnityEngine.Object[] allComponents = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (allComponents.Length == 0) {
                report.Add($"No {componentType.Name} components found", true, 
                    $"No {componentType.Name} component found in the scene. Use 'Check' to set one up automatically.");
            } else {
                foreach (UnityEngine.Object component in allComponents) {
                    if (component is UnityEngine.Component unityComponent) {
                        string componentName = unityComponent.gameObject.name;
                        string sceneName = unityComponent.gameObject.scene.name;
                        bool isActive = unityComponent.gameObject.activeInHierarchy;
                        string activeText = isActive ? " (Active)" : " (Inactive)";

                        report.AddOptionalAction(
                            $"{componentType.Name}: {componentName}{activeText} in {sceneName}",
                            $"This GameObject has a {componentType.Name} component attached.",
                            null,
                            unityComponent.gameObject,
                            null,
                            "Select");
                    }
                }
            }

            SetResultsExpanded(true);
        }

        void LoadAssetIcons() {
            DisposeAssetIcons();
            foreach (AssetIntegration asset in AssetRegistry.Assets) {
                if (string.IsNullOrEmpty(asset.IconPath)) {
                    continue;
                }
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(asset.IconPath);
                if (icon != null) {
                    _assetIcons[asset.Id] = icon;
                }
            }
        }

        void DisposeAssetIcons() {
            foreach (Texture2D icon in _assetIcons.Values) {
                if (icon != null && icon.hideFlags == HideFlags.HideAndDontSave) {
                    DestroyImmediate(icon);
                }
            }
            _assetIcons.Clear();
        }

        Texture2D GetAssetIcon(string assetId) {
            _assetIcons.TryGetValue(assetId, out Texture2D icon);
            return icon;
        }

        static bool HasValidFolder(string folder) {
            if (string.IsNullOrEmpty(folder)) return false;
            // Safety: never consider Assets root or Packages as valid folders for operations
            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (normalized == "Assets" || normalized == "Packages" || normalized.StartsWith("Packages/")) return false;
            return AssetDatabase.IsValidFolder(folder);
        }

        static string GetInstalledFolder(AssetIntegration asset) {
            if (!string.IsNullOrEmpty(asset.InstalledFolder) && AssetDatabase.IsValidFolder(asset.InstalledFolder)) {
                return asset.InstalledFolder;
            }
            return asset.AssetFolder;
        }

        static void SelectFolderInProject(string folder) {
            if (!HasValidFolder(folder)) return;
            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);
            if (obj == null) return;

            int instanceId = obj.GetInstanceID();
            System.Type projectBrowserType = System.Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
            if (projectBrowserType != null) {
                var showMethod = projectBrowserType.GetMethod("ShowFolderContents", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (showMethod != null) {
                    EditorWindow projectWindow = EditorWindow.GetWindow(projectBrowserType, false, null, false);
                    if (projectWindow != null) {
                        showMethod.Invoke(projectWindow, new object[] { instanceId, true });
                        return;
                    }
                }
            }
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        void ExecuteFix(AssetIntegration asset, ValidationEntry entry) {
            if (entry?.FixAction == null) return;
            if (!entry.NeedsFix) {
                Debug.Log($"Fix for '{entry.Description}' skipped - issue already resolved.");
                RefreshValidations(asset);
                return;
            }
            string description = entry.Description;
            try {
                entry.FixAction.Invoke();
                RefreshValidations(asset);

                if (_validationReports.TryGetValue(asset.Id, out ValidationReport newReport)) {
                    ValidationEntry updatedEntry = newReport.Entries.FirstOrDefault(e => e.Description == description);
                    if (updatedEntry != null) {
                        if (updatedEntry.ShowAction != null) {
                            updatedEntry.ShowAction.Invoke();
                        } else if (updatedEntry.ShowTarget != null) {
                            Selection.activeObject = updatedEntry.ShowTarget;
                            EditorGUIUtility.PingObject(updatedEntry.ShowTarget);
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.LogError($"Fix for '{entry.Description}' failed: {ex.Message}");
            }
        }

        void RefreshValidations(AssetIntegration asset) {
            if (_isSingleAssetMode) {
                RunValidation(asset);
            } else {
                VerifyAllSetups();
            }
        }

        static bool ConfirmOptionalAction(ValidationEntry entry) {
            string message = !string.IsNullOrEmpty(entry.Details) ? entry.Details : entry.Description;
            return EditorUtility.DisplayDialog("Confirm Action", message, "Execute", "Cancel");
        }

        static void DrawDetailsButton(ValidationEntry entry) {
            bool hasDetails = !string.IsNullOrEmpty(entry.Details);
            using (new EditorGUI.DisabledScope(!hasDetails)) {
                if (GUILayout.Button("Details", GUILayout.Width(60f)) && hasDetails) {
                    EditorUtility.DisplayDialog("Validation Details", entry.Details, "OK");
                }
            }
        }

        void DrawStatusBadge(string text, bool isWarning = false) {
            Color badgeColor = isWarning ? WarningColor : SuccessColor;
            GUIContent content = new GUIContent(text);
            Vector2 size = _statusBadgeStyle.CalcSize(content);
            Rect badgeRect = GUILayoutUtility.GetRect(size.x, size.y, GUILayout.ExpandWidth(false));
            Color prevBgColor = GUI.backgroundColor;
            Color prevTextColor = GUI.color;
            GUI.backgroundColor = badgeColor;
            GUI.Box(badgeRect, GUIContent.none, EditorStyles.helpBox);
            GUI.backgroundColor = prevBgColor;
            GUI.color = badgeColor;
            GUI.Label(badgeRect, content, _statusBadgeStyle);
            GUI.color = prevTextColor;
        }

        void CreateStyles() {
            if (_statusBadgeStyle == null) {
                _statusBadgeStyle = new GUIStyle(EditorStyles.label) {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = SuccessColor },
                    hover = { textColor = SuccessColor },
                    active = { textColor = SuccessColor },
                    focused = { textColor = SuccessColor },
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                    padding = new RectOffset(8, 8, 3, 3)
                };
            }
            if (_validationLabelStyle == null) {
                _validationLabelStyle = new GUIStyle(EditorStyles.label) {
                    alignment = TextAnchor.MiddleLeft
                };
            }
            if (_validationStatusStyle == null) {
                _validationStatusStyle = new GUIStyle(EditorStyles.label) {
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_tableHeaderLeftStyle == null) {
                _tableHeaderLeftStyle = new GUIStyle(EditorStyles.toolbar) {
                    alignment = TextAnchor.MiddleLeft
                };
            }
            if (_tableHeaderCenterStyle == null) {
                _tableHeaderCenterStyle = new GUIStyle(EditorStyles.toolbar) {
                    alignment = TextAnchor.MiddleCenter
                };
            }
            if (_verifyButtonStyle == null) {
                _verifyButtonStyle = new GUIStyle(GUI.skin.button) {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        void RemoveAssetFromProject(AssetIntegration asset) {
            string installedFolder = AssetRuntimeState.Get(asset).InstalledFolder ?? GetInstalledFolder(asset);
            string folderInfo = HasValidFolder(installedFolder) ? $"\n\nAsset folder: {installedFolder}" : "";
            
            if (!EditorUtility.DisplayDialog(
                "Clean Up & Remove",
                $"Clean up {asset.DisplayName} from the project?\n\nThis will remove any traces of {asset.DisplayName}:\n• Volume overrides\n• Render features from URP assets\n• Scene components{folderInfo}\n\nThis action cannot be undone.",
                "Clean Up",
                "Cancel")) {
                return;
            }

            int removedOverrides = 0;
            int removedFeatures = 0;
            int removedComponents = 0;

            if (!string.IsNullOrEmpty(asset.VolumeComponentType) && TypeUtility.TryGetType(asset.VolumeComponentType, out Type volumeType)) {
                removedOverrides = RemoveVolumeOverrides(volumeType);
            }

            if (asset.RenderFeatureTypeNames != null) {
                foreach (string featureTypeName in asset.RenderFeatureTypeNames) {
                    if (TypeUtility.TryGetType(featureTypeName, out Type featureType)) {
                        removedFeatures += RemoveRenderFeatures(featureType);
                    }
                }
            }

            if (asset.CleanupRenderFeatureTypeNames != null) {
                foreach (string featureTypeName in asset.CleanupRenderFeatureTypeNames) {
                    if (TypeUtility.TryGetType(featureTypeName, out Type featureType)) {
                        removedFeatures += RemoveRenderFeatures(featureType);
                    }
                }
            }

            if (!string.IsNullOrEmpty(asset.SceneComponentType) && TypeUtility.TryGetType(asset.SceneComponentType, out Type sceneType)) {
                removedComponents += RemoveSceneComponents(sceneType);
            }

            if (asset.AdditionalSceneComponentTypes != null) {
                foreach (string additionalType in asset.AdditionalSceneComponentTypes) {
                    if (TypeUtility.TryGetType(additionalType, out Type type)) {
                        removedComponents += RemoveSceneComponents(type);
                    }
                }
            }

            int removedRenderers = 0;
            if (asset.CleanupRendererDataAssetNames != null) {
                foreach (string rendererName in asset.CleanupRendererDataAssetNames) {
                    removedRenderers += RemoveRendererDataFromPipelines(rendererName);
                }
            }

            Debug.Log($"[{asset.DisplayName}] Cleaned up: {removedOverrides} volume override(s), {removedFeatures} render feature(s), {removedRenderers} renderer(s), {removedComponents} scene component(s)");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (HasValidFolder(installedFolder)) {
                if (EditorUtility.DisplayDialog(
                    "Clean Up Complete",
                    $"{asset.DisplayName} traces have been removed from the project.\n\nDo you also want to delete the asset folder?\n\nPath: {installedFolder}",
                    "Delete Folder",
                    "Keep Folder")) {
                    AssetDatabase.DeleteAsset(installedFolder);
                    AssetDatabase.Refresh();
                    Debug.Log($"[{asset.DisplayName}] Asset folder deleted: {installedFolder}");
                }
            } else {
                EditorUtility.DisplayDialog(
                    "Clean Up Complete",
                    $"{asset.DisplayName} traces have been removed from the project.",
                    "OK");
            }
        }

        int RemoveVolumeOverrides(Type volumeComponentType) {
            int count = 0;
            
            string[] volumeProfileGuids = AssetDatabase.FindAssets("t:VolumeProfile");
            foreach (string guid in volumeProfileGuids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null) continue;

                for (int i = profile.components.Count - 1; i >= 0; i--) {
                    VolumeComponent component = profile.components[i];
                    if (component != null && component.GetType() == volumeComponentType) {
                        profile.components.RemoveAt(i);
                        DestroyImmediate(component, true);
                        EditorUtility.SetDirty(profile);
                        count++;
                    }
                }
            }

            Volume[] sceneVolumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Volume volume in sceneVolumes) {
                if (volume.sharedProfile == null) continue;
                
                for (int i = volume.sharedProfile.components.Count - 1; i >= 0; i--) {
                    VolumeComponent component = volume.sharedProfile.components[i];
                    if (component != null && component.GetType() == volumeComponentType) {
                        volume.sharedProfile.components.RemoveAt(i);
                        DestroyImmediate(component, true);
                        EditorUtility.SetDirty(volume);
                        count++;
                    }
                }
            }

            return count;
        }

        int RemoveRenderFeatures(Type renderFeatureType) {
            int count = 0;

            string[] rendererGuids = AssetDatabase.FindAssets("t:ScriptableRendererData");
            foreach (string guid in rendererGuids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null) continue;

                var features = rendererData.rendererFeatures;
                bool modified = false;
                for (int i = features.Count - 1; i >= 0; i--) {
                    ScriptableRendererFeature feature = features[i];
                    if (feature != null && feature.GetType() == renderFeatureType) {
                        feature.SetActive(false);
                        features.RemoveAt(i);
                        DestroyImmediate(feature, true);
                        modified = true;
                        count++;
                    }
                }
                if (modified) {
                    EditorUtility.SetDirty(rendererData);
                    AssetDatabase.SaveAssetIfDirty(rendererData);
                }
            }

            return count;
        }

        int RemoveSceneComponents(Type componentType) {
            int count = 0;

            UnityEngine.Object[] components = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (UnityEngine.Object obj in components) {
                if (obj is UnityEngine.Component component) {
                    GameObject go = component.gameObject;
                    var scene = go.scene;
                    DestroyImmediate(component);
                    
                    if (go != null && go.GetComponents<UnityEngine.Component>().Length == 1) {
                        DestroyImmediate(go);
                    }
                    
                    if (scene.IsValid()) {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    }
                    
                    count++;
                }
            }

            return count;
        }

        int RemoveRendererDataFromPipelines(string rendererAssetName) {
            int count = 0;

            var fieldInfo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldInfo == null) return count;

            string[] pipelineGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            foreach (string guid in pipelineGuids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UniversalRenderPipelineAsset pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipelineAsset == null) continue;

                var rendererList = fieldInfo.GetValue(pipelineAsset) as ScriptableRendererData[];
                if (rendererList == null || rendererList.Length <= 1) continue;

                var indicesToRemove = new System.Collections.Generic.List<int>();
                for (int i = 1; i < rendererList.Length; i++) {
                    ScriptableRendererData rendererData = rendererList[i];
                    if (rendererData != null && rendererData.name == rendererAssetName) {
                        indicesToRemove.Add(i);
                    }
                }

                if (indicesToRemove.Count > 0) {
                    var newList = new ScriptableRendererData[rendererList.Length - indicesToRemove.Count];
                    for (int j = 0, k = 0; j < rendererList.Length; j++) {
                        if (!indicesToRemove.Contains(j)) {
                            newList[k++] = rendererList[j];
                        }
                    }
                    fieldInfo.SetValue(pipelineAsset, newList);
                    EditorUtility.SetDirty(pipelineAsset);
                    AssetDatabase.SaveAssetIfDirty(pipelineAsset);
                    count += indicesToRemove.Count;
                }
            }

            return count;
        }

    }
}
