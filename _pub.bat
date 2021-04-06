echo off
REM SETLOCAL ENABLEDELAYEDEXPANSION

cd "D:\GitHub\StoreLake\"
IF NOT %ERRORLEVEL%==0 (call :REGERROR "change directory failed.")

CALL :DropFolder ".vs"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake\bin\Debug\net48"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake\bin\Debug"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake\bin"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake\obj\Debug\net48"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake\obj\Debug"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake\obj"

CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk\bin\Debug\net48"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk\bin\Debug"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk\bin"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk\obj\Debug\net48"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk\obj\Debug"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk\obj"

CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\bin\Debug\net48"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\bin\Debug"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\bin"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\obj\Debug\net48"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\obj\Debug"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\obj"

CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Test.ConsoleApp\bin"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Test.ConsoleApp\obj\Debug\net48"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Test.ConsoleApp\obj\Debug"
CALL :DropFolder "D:\GitHub\StoreLake\src\StoreLake.Test.ConsoleApp\obj"

REM EXIT /B


"D:\GitHub\StoreLake\src\ThirdParty\StoreLake.Versioning.exe" /root=D:\GitHub\StoreLake\
IF NOT %ERRORLEVEL%==0 (call :REGERROR "version incrementing failed.")


dotnet build --no-incremental
IF NOT %ERRORLEVEL%==0 (call :REGERROR "build failed.")

dotnet pack --verbosity normal --force --include-symbols
IF NOT %ERRORLEVEL%==0 (call :REGERROR "pack failed.")


FOR %%F IN ("D:\GitHub\StoreLake\src\StoreLake\bin\Debug\*.symbols.nupkg"
           ,"D:\GitHub\StoreLake\src\StoreLake.Sdk\bin\Debug\*.symbols.nupkg"
		   ,"D:\GitHub\StoreLake\src\StoreLake.Sdk.Cli\bin\Debug\*.symbols.nupkg"
) DO ( 
	call :REGPUSH %%F
)

REM %userprofile%\.nuget\packages
REM C:\Users\xxx\.nuget\packages\storelake\1.0.19
REM https://superuser.com/questions/489240/how-to-get-filename-only-without-path-in-windows-command-line
REM https://codestory.de/11623/batch-loops

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
CALL :GetPackageVersion %1 packageversion ErrorFlag
IF NOT %ErrorFlag%==0 (
	call :REGERROR "'GetPackageVersion' failed:" %ErrorFlag%
)

ECHO the packageversion is %packageversion%
REM Increment 'PackageVersion' in common.props before pushing
dotnet nuget push --source "StoreLake" --api-key az %1
ECHO ErrLev: %ERRORLEVEL%
IF NOT %ERRORLEVEL%==0 (
	call :REGERROR "push failed."
)

REM CALL :UpdateLocalNugetCache %packageversion%

echo ===============================
goto :EOF
REM -------------------------------------------------------------

REM -------------------------------------------------------------
:GetPackageVersion
:: %1 - <INPUT> Full file name of the nuget package file. '"D:\GitHub\StoreLake\src\StoreLake.Sdk\bin\Debug\StoreLake.1.0.22.symbols.nupkg'
:: %2 - <OUTPUT> PackageVersion.
:: %3 - <OUTPUT> ErrorFlag. 0=NoError, 1:Error

REM powershell write-host -fore Cyan This is Cyan %1 text
REM color 48
echo : %1
::echo : %~dp1
::echo : %~n1
::echo : %~x1
::echo : %~nx1
set "string=%~n1"
set dot_count=0
for %%a in (%string:.= %) do set /a dot_count+=1
echo dot_count:%dot_count% : %1

:: comment
SET errorflag=0
SET v=%~n1
SET v=%v:.=,%
FOR /F "tokens=1,2,3,4,5,6,7 delims=," %%A IN ("%v%") DO (
	echo Tokens= %%A-%%B-%%C-%%D-%%E-%%F-%%G
	IF %dot_count%==5 (
		SET packageversion=%%B.%%C.%%D
	) ELSE (
		IF %dot_count%==6 (
			SET packageversion=%%C.%%D.%%E
		) ELSE (
			IF %dot_count%==7 (
				SET packageversion=%%D.%%E.%%F
			) ELSE (
				SET "%errorflag%=1"
				ECHO Error package_version_dot_count=%dot_count%
			)
		)
	)
)
REM echo err: %ERRORLEVEL%

REM ECHO %packageversion%
REM ECHO errorflag:%errorflag%

SET "%2=%packageversion%"
SET "%3=%errorflag%"


REM color
EXIT /B
REM -------------------------------------------------------------

REM -------------------------------------------------------------
:UpdateLocalNugetCache
:: %1 - <INPUT> PackageVersion
ECHO UpdateLocalNugetCache(%1)

SET folder_ver=%userprofile%\.nuget\packages\StoreLake\%1\lib\net48
ECHO fn:%folder_ver%

IF EXIST "%folder_ver%" (
	ECHO Folder '%folder_ver%' exists.
) ELSE (
	MKDIR %folder_ver%
	ECHO mkdir_result=%ERRORLEVEL%
	IF NOT %ERRORLEVEL%==0 (
		call :REGERROR "mkdir failed:" %folder_ver%
	)
)

REM + .\lib\net48+ StoreLake.dll, StoreLake.pdb
REM + .\.nupkg.metadata
REM + .\storelake.1.0.45.nupkg
REM + .\storelake.1.0.45.nupkg.sha512
REM + .\storelake.nuspec


EXIT /B
REM -------------------------------------------------------------


REM -------------------------------------------------------------
:DropFolder
:: %1 - <INPUT> PackageVersion
ECHO DropFolder(%1)

IF NOT EXIST "%1" (
	REM ECHO Files folder '%1' does not exists.
) ELSE (
	attrib -h -r -s %1\*.* /s /d
	REM ECHO __ delete files from: %1
	del /f /s /q %1\*.*

	REM ECHO +r
    REM attrib +r "%1\*.*" /s /d
	REM ECHO -r
	REM attrib -r "%1\*.*" /s /d
	REM ECHO __ drop folder: %1
	rmdir "%1"
	REM dir /x %1
)
REM ECHO drop done.

EXIT /B
REM -------------------------------------------------------------
