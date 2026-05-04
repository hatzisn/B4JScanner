@echo off
setlocal

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT=B4JScanner.exe
set REFS=/r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Xml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll

%CSC% /target:winexe /out:%OUT% /win32icon:icon.ico %REFS% Program.cs Models.cs B4JProjectParser.cs LibraryResolver.cs JarAnalyzer.cs JavaSourceScanner.cs SbomWriter.cs MdWriter.cs HtmlWriter.cs OsvResultParser.cs MainForm.cs Config.cs

if errorlevel 1 (
    echo Build FAILED
    exit /b 1
)

echo Build OK: %OUT%
