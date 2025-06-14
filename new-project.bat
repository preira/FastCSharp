@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

REM Default project type
SET PROJECT_TYPE=classlib

REM Check if project name is provided
IF "%~1"=="" (
    echo Usage: create_project.bat ProjectName [-t ProjectType]
    echo Example: create_project.bat CDK_Core -t classlib
    EXIT /B 1
)

REM Parse arguments
SET PROJECT_NAME=%1
SHIFT

:parse_args
IF "%~1"=="" GOTO done_args
IF /I "%~1"=="-t" (
    SHIFT
    SET PROJECT_TYPE=%1
)
SHIFT
GOTO parse_args

:done_args

SET SOLUTION_NAME=%PROJECT_NAME%.sln
SET TEST_PROJECT=%PROJECT_NAME%.Tests

REM Create Solution
dotnet new sln -o %SOLUTION_NAME%
cd %SOLUTION_NAME%

REM Rename the solution file from .sln.sln to .sln
copy %SOLUTION_NAME%.sln %SOLUTION_NAME%
del %SOLUTION_NAME%.sln

REM Create Projects
dotnet new %PROJECT_TYPE% -o %PROJECT_NAME%
dotnet new xunit -o %TEST_PROJECT%

REM Add Projects to Solution
dotnet sln add .\%PROJECT_NAME%
dotnet sln add .\%TEST_PROJECT%

REM Add Reference in Test Project
cd %TEST_PROJECT%
dotnet add reference ..\%PROJECT_NAME%

REM Move to CFT_Build and add solution references
cd ..\..\
cd BuildAll
dotnet sln add ..\%SOLUTION_NAME%\%PROJECT_NAME%
dotnet sln add ..\%SOLUTION_NAME%\%TEST_PROJECT%

echo Project setup complete!
