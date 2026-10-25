#!/bin/bash
set -e

# Program.cs resolves CONNECTION_SLAG/CONNECTION_AUTH or the shared DB_* variables.
# Do not put secrets in CLI arguments (process lists) or shell trace output.
echo "Applying migrations for SlagModeContext..."
dotnet ef database update \
    --context SlagModeContext \
    --project DBStructure \
    --startup-project AglomSlagServer

echo "Applying migrations for AuthContext..."
dotnet ef database update \
    --context AuthContext \
    --project DBStructure \
    --startup-project AglomSlagServer

echo "All migrations applied."
