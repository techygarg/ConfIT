#!/bin/bash -e

if [ "$#" -ne 2 ]; then
    echo "Usage: $0 <version> <api-key>"
    exit 1
fi

dotnet nuget push ../src/ConfIT/ConfIT.$1.nupkg --source 'https://api.nuget.org/v3/index.json' --api-key $2 --skip-duplicate