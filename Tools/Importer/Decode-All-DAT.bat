@echo off
setlocal
set "ROOT=%~dp0..\.."
set "SRC=%ROOT%\ImportedDatabases\NativeRuntime"
set "OUT=%ROOT%\Data\NativeDecodeReports\ManualRun"
if not exist "%SRC%" (
  echo Native runtime source folder not found: %SRC%
  echo Expected Database Version 0003 native runtime archive to be applied first.
  pause
  exit /b 1
)
python "%ROOT%\Tools\Importer\MajorMudDatDecoder.py" "%SRC%" --out "%OUT%"
pause
