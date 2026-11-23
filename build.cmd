@echo off
rem Compile BureauCache.exe avec le compilateur C# fourni avec Windows (.NET Framework 4).
rem Tous les fichiers .cs du dossier src (et de ses sous-dossiers) sont inclus automatiquement.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set REFS=-r:Microsoft.CSharp.dll -r:System.Core.dll -r:Microsoft.VisualBasic.dll
if exist BureauCache.ico goto compile
"%CSC%" -nologo -target:winexe -optimize -warn:4 -out:BureauCache.exe %REFS% -recurse:src\*.cs
if not "%ERRORLEVEL%"=="0" goto failed
start "" /wait "%CD%\BureauCache.exe" --make-icon BureauCache.ico
if not "%ERRORLEVEL%"=="0" goto failed
if not exist BureauCache.ico goto failed

:compile
"%CSC%" -nologo -target:winexe -optimize -warn:4 -win32icon:BureauCache.ico -out:BureauCache.exe %REFS% -recurse:src\*.cs
if not "%ERRORLEVEL%"=="0" goto failed
echo Compilation terminee : BureauCache.exe
exit /b 0

:failed
echo Echec de la compilation.
exit /b 1
