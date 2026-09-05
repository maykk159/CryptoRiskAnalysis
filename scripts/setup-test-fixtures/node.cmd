@echo off
>>"%SETUP_TEST_LOG%" echo node:%*
if "%~1"=="--version" (
  echo %SETUP_TEST_NODE%
  exit /b 0
)
if "%~1"=="--print" (
  echo x64
  exit /b 0
)
exit /b 1
