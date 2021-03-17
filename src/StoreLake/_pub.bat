echo off

dotnet build
IF NOT %ERRORLEVEL%==0 (call :REGERROR "build failed.")
REM  --include-symbols  
dotnet pack --verbosity normal --force
IF NOT %ERRORLEVEL%==0 (call :REGERROR "pack failed.")

xcopy .\bin\Debug\*.nupkg ..\..\..\..\Helpline\Current\packages\Dibix.TestStore.1.0.0\ /Y /f /k /r /v
IF NOT %ERRORLEVEL%==0 (call :REGERROR "copy failed.")

xcopy .\bin\Debug\net48\*.* ..\..\..\..\Helpline\Current\packages\Dibix.TestStore.1.0.0\lib\net48\ /Y /f /k /r /v
IF NOT %ERRORLEVEL%==0 (call :REGERROR "copy failed.")

rem dotnet nuget push --source "StoreLake" --api-key az "D:\GitHub\StoreLake\src\StoreLake\bin\Debug\StoreLake.1.0.0.nupkg"



echo "finish!"
pause
exit
REM -------------------------------------------------------------
REM -------------------------------------------------------------



REM -------------------------------------------------------------
:REGERROR

echo ===============================
echo ======== ERROR ! ==========
echo %1
echo ===============================
pause
exit 
REM -------------------------------------------------------------