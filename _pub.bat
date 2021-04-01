echo off
REM SETLOCAL ENABLEDELAYEDEXPANSION

cd "D:\GitHub\StoreLake\"
IF NOT %ERRORLEVEL%==0 (call :REGERROR "change directory failed.")

"D:\GitHub\StoreLake\src\ThirdParty\StoreLake.Versioning.exe" /root=D:\GitHub\StoreLake\
IF NOT %ERRORLEVEL%==0 (call :REGERROR "version incrementing failed.")

REM del /S /Q "D:\GitHub\StoreLake\src\StoreLake\bin\Debug\*.*"
rmdir "D:\GitHub\StoreLake\src\StoreLake\bin\Debug" /S /Q
rmdir "D:\GitHub\StoreLake\src\StoreLake.Sdk\bin\Debug" /S /Q
rmdir "D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\bin\Debug" /S /Q
rmdir "D:\GitHub\StoreLake\src\StoreLake.Test.ConsoleApp\bin\Debug" /S /Q

dotnet build
IF NOT %ERRORLEVEL%==0 (call :REGERROR "build failed.")

dotnet pack --verbosity normal --force
IF NOT %ERRORLEVEL%==0 (call :REGERROR "pack failed.")


FOR %%F IN ("D:\GitHub\StoreLake\src\StoreLake\bin\Debug\*.nupkg"
           ,"D:\GitHub\StoreLake\src\StoreLake.Sdk\bin\Debug\*.nupkg"
		   ,"D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\bin\Debug\*.nupkg"
		   ,"D:\GitHub\StoreLake\src\StoreLake.Test.ConsoleApp\bin\Debug\*.nupkg"
) DO ( 
	call :REGPUSH %%F
)

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

REM -------------------------------------------------------------
:REGPUSH

echo ===============================
echo ======== PUSH ! ==========
echo %1

ECHO the file is %1
REM Increment 'PackageVersion' in common.props before pushing
dotnet nuget push --source "StoreLake" --api-key az %1
ECHO ErrLev: %ERRORLEVEL%
IF NOT %ERRORLEVEL%==0 (
	call :REGERROR "push failed."
)

echo ===============================
goto :EOF
REM -------------------------------------------------------------