MajorMUD Reforged - Editor Startup Fix V2
=========================================

Problem fixed
-------------
The editor contains a read-only WPF TextBox bound to EditorViewModel.BaseRecordJson.
WPF TextBox.Text defaults to TwoWay, but BaseRecordJson has a private setter. That
causes startup to fail with:

  A TwoWay or OneWayToSource binding cannot work on the read-only property
  'BaseRecordJson'...

This fix changes the binding to:

  Text="{Binding BaseRecordJson, Mode=OneWay}"

Why V2
------
The previous fix rebuilt the project but did not publish the fresh Release copy.
If you launched Release\MajorMudReforgedApp\MajorMudReforged.exe afterward, Windows
could still be running the old published app with the bad XAML resource.

How to use
----------
1. Extract this whole top folder anywhere.
2. Double-click APPLY_EDITOR_STARTUP_FIX_V2.bat.
3. Wait for BOTH Build and Publish to succeed.
4. For the first test, double-click START_FIXED_APP.bat.

The fixer backs up EditorView.xaml before changing it, clears Reforged.App bin/obj,
builds, publishes, verifies the new EXE exists, and records the repo location.

Manual fallback
---------------
A corrected copy of EditorView.xaml from the repo you supplied is included under:

PATCH\Apps\Source\Reforged.App\Views\EditorView.xaml

Normally you should use the BAT instead of copying it manually.
