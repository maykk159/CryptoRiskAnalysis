@echo off
>>"%SETUP_TEST_LOG%" echo npm:%*
if "%~1"=="--version" (
  echo %SETUP_TEST_NPM%
  exit /b 0
)
if "%~1"=="%SETUP_TEST_FAIL%" (
  echo Simulated npm failure.
  exit /b 42
)
if "%~1"=="run" if "%~2"=="%SETUP_TEST_FAIL%" (
  echo Simulated npm script failure.
  exit /b 42
)
if "%~1"=="ci" (
  if not exist node_modules\.bin mkdir node_modules\.bin
  copy /y package-lock.json node_modules\.package-lock.json >nul
  for %%b in (vite tsc vitest eslint prettier) do echo @echo off>node_modules\.bin\%%b.cmd
)
exit /b 0
