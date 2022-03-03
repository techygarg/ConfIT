#!/bin/bash -e

dotnet nuget push ../src/ConfIT/ConfIT.$1.nupkg --source 'https://api.nuget.org/v3/index.json' --api-key $2 --skip-duplicate