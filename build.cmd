@echo off
rem Compile BureauCache.exe avec le compilateur C# fourni avec Windows (.NET Framework 4).
rem Tous les fichiers .cs du dossier src (et de ses sous-dossiers) sont inclus automatiquement.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set REFS=-r:Microsoft.CSharp.dll -r:System.Core.dll -r:Microsoft.VisualBasic.dll
set ICON=
if exist BureauCache.ico set ICON=-win32icon:BureauCache.ico
"%CSC%" -nologo -target:winexe -optimize -warn:4 %ICON% -out:BureauCache.exe %REFS% -recurse:src\*.cs
if errorlevel 1 (echo. & echo Echec de la compilation. & exit /b 1)
if not exist BureauCache.ico (
  BureauCache.exe --make-icon BureauCache.ico
  "%CSC%" -nologo -target:winexe -optimize -win32icon:BureauCache.ico -out:BureauCache.exe %REFS% -recurse:src\*.cs
)
echo Compilation terminee : BureauCache.exe
