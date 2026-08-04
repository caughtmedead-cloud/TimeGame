**************************************
*          KRONNECT HUB              *
* Created by Ramiro Oliva (Kronnect) * 
*          README FILE               *
**************************************


What is the Kronnect Hub?
--------------------------

The Kronnect Hub is a Unity Editor tool that centralizes the management and configuration of all Kronnect assets in your project.
It provides a unified interface to verify, configure, and maintain your visual effects assets compatible with URP (Universal Render Pipeline).


How to Use the Kronnect Hub
----------------------------

1) Open the Hub:
   Select from the menu: Window > Kronnect > Hub

2) Verify asset setup:
   • The Hub automatically verifies all installed assets when opened
   • Use the "Verify All Assets" button to re-verify all assets
   • Use the "Check Setup" button on each asset to verify its individual configuration

3) Main features:
   
   a) Check Setup:
      Verifies that the active URP asset includes all required render features and settings.
      The system validates:
      - URP asset is assigned in Project Settings / Graphics
      - URP asset is assigned in Project Settings / Quality
      - URP asset contains a valid Renderer
      - Required render features are added
      - Volume components are configured (if applicable)
      - Scene components exist (if applicable)
      - Depth texture is enabled (if required)
      - Rendering mode is correct (Forward/Deferred)
      - etc.
   
   b) Find in Scene:
      Locates all volumes or components of the asset in the current scene.
      Useful for quickly finding where effects are configured.
   
   c) Open Folder:
      Opens the asset folder in the Project window for quick access.
   
   d) Clean Up & Remove:
      Completely cleans and removes the asset from the project:
      - Removes volume overrides
      - Removes render features from URP assets
      - Removes scene components
      - Optionally deletes the asset folder

4) Validation results:
   • Results are shown in the expandable "Results" section at the bottom
   • Each validation shows its status: ✅ (valid), ⚠ (warning), or FAIL (failure)
   • You can use the "Fix" button to automatically fix detected issues
   • You can use the "Show" button to locate the related object in the inspector
   • You can use the "Fix All" button to fix all issues for all assets

5) Import assets:
   • If an asset is not installed but available in cache, an "Import" button will appear
   • If not in cache, an "Asset Store" button will appear to open the asset page



Help & Support
--------------

Have any questions or issues?

• Support Forum: https://kronnect.com/support
• Discord: https://discord.gg/EH2GMaM
• Email: contact@kronnect.com
• Twitter: @Kronnect

If you like the Kronnect Hub, please rate it. It encourages us to keep improving it!


Requirements
------------

• Unity 6.0
• Universal Render Pipeline (URP)
• Kronnect assets compatible with URP


Notes
-----

• The Hub works only in the Unity Editor, it's not included in your builds
• Validations run automatically when opening the Hub
• Changes made by the Hub may require saving assets or scenes
• Some automatic fixes may require user confirmation


Changes
-------

Version 1.1:
- Added button to show package in the local Asset Store cache
- Added button to show the asset in the Asset Store
- Added additional validations & cleanup tasks for Volumetric Lights 2
