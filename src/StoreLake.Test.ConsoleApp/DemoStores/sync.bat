echo off

xcopy ..\..\..\..\..\Helpline\Current\bin\Debug\Output\database\HelplineData.dll .\ /Y /f /k /r /v
IF NOT %ERRORLEVEL%==0 (call :REGERROR "copy failed.")

xcopy ..\..\..\..\..\Helpline\Current\bin\Debug\Output\database\HelplineData.TestStore.dll .\ /Y /f /k /r /v
IF NOT %ERRORLEVEL%==0 (call :REGERROR "copy failed.")

xcopy ..\..\..\..\..\Helpline\Current\bin\Debug\Output\database\HelplineData.TestStore.pdb .\ /Y /f /k /r /v
IF NOT %ERRORLEVEL%==0 (call :REGERROR "copy failed.")

xcopy ..\..\..\..\..\Helpline\Current\bin\Debug\Output\database\SLM.Database.Data.TestStore.dll .\ /Y /f /k /r /v
IF NOT %ERRORLEVEL%==0 (call :REGERROR "copy failed.")

xcopy ..\..\..\..\..\Helpline\Current\bin\Debug\Output\database\SLM.Database.Data.TestStore.pdb .\ /Y /f /k /r /v
IF NOT %ERRORLEVEL%==0 (call :REGERROR "copy failed.")

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
REM ----------