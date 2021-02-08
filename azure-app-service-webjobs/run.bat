REM *** DOWNLOAD AND BUILD THE PROJECT ***
rmdir %TEMP%\app /s /q
mkdir %TEMP%\app
cd %TEMP%\app
git clone https://github.com/Moesif/ApimEventProcessor
cd ApimEventProcessor\src\ApimEventProcessor
dotnet clean
nuget install packages.config
dotnet build --configuration "Release"
cd bin\Release

ApimEventProcessor.exe
