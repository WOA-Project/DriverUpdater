@echo off

msbuild /m /t:restore,driverupdater:publish /p:Platform=x86 /p:RuntimeIdentifier=win-x86 /p:PublishDir="%CD%\publish\artifacts\win-x86\CLI" /p:PublishSingleFile=true /p:PublishTrimmed=false /p:Configuration=Release DriverUpdater.sln