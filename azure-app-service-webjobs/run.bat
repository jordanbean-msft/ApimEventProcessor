REM *** DOWNLOAD AND BUILD THE PROJECT ***
mkdir %TEMP%\app
cd %TEMP%\app
git clone https://github.com/Moesif/ApimEventProcessor
cd ApimEventProcessor\src\ApimEventProcessor
nuget install packages.config
dotnet build
cd bin\Debug

REM ** LAUNCH THE TASK ***
ApimEventProcessor.exe
