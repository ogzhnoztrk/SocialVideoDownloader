#!/usr/bin/env bash
cd "$(dirname "$0")"
if command -v cmd.exe >/dev/null 2>&1; then
  cmd.exe //c Baslat.cmd
  exit $?
fi

dotnet run --project SocialVideoDownloader.Web/SocialVideoDownloader.Web.csproj &
sleep 2
dotnet run --project SocialVideoDownloader.Tray/SocialVideoDownloader.Tray.csproj &
