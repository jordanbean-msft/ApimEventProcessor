REM **** RUN IT ***
SET APIMEVENTS-EVENTHUB-NAME=enter-your-azure-eventhub-name-here
SET APIMEVENTS-EVENTHUB-CONNECTIONSTRING=Endpoint=sb://enter-eventhub-url-here/;SharedAccessKeyName=enter-shared-access-key-name;SharedAccessKey=enter-shared-access-key
SET APIMEVENTS-STORAGEACCOUNT-NAME=enter-azure-storage-account-name
SET APIMEVENTS-STORAGEACCOUNT-KEY=enter-azure-storage-account-key
SET APIMEVENTS-MOESIF-APPLICATION-ID=enter-moesif-application-id
SET APIMEVENTS-MOESIF-SESSION-TOKEN=set-if-you-wish-to
SET APIMEVENTS-MOESIF-API-VERSION=v1

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
