:: RML Mod Build Script by TeKGameR

:: Disabling echoing
@echo off
:: Defining the window title
title RML Mod Build Script
:: Retrieving the current folder name
for %%* in (.) do set foldername=%%~n*
:: Creating a folder to contain temporary files for the build
mkdir "build"
:: Copying the solution directory in the "build" folder except some files and folders.
robocopy "%foldername%" "build" /E /XF *.csproj *.csproj.user *.rmod /XD bin obj Properties .vs
:: Checking if a .rmod with the same name already exists and if it does, delete it.
if exist "%foldername%.rmod" ( del "%foldername%.rmod" )
:: Zipping the "build" folder. (.rmod are just zipped files)
powershell "[System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem');[System.IO.Compression.ZipFile]::CreateFromDirectory(\"build\", \"%foldername%.rmod\", 0, 0)"
:: Deleting the "build" folder
rmdir /s /q "build"
:: Build succeeded!