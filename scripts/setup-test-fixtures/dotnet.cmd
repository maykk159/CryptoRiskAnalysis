@echo off
>>"%SETUP_TEST_LOG%" echo dotnet:%*
if "%~1"=="--version" (
  echo %SETUP_TEST_DOTNET%
  exit /b 0
)
if "%~1"=="%SETUP_TEST_FAIL%" (
  echo Simulated dotnet failure.
  exit /b 42
)
if "dotnet-%~1"=="%SETUP_TEST_FAIL%" (
  echo Simulated dotnet failure.
  exit /b 42
)
exit /b 0
