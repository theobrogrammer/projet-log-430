#!/bin/bash
docker compose logs -f api | sed 's/^brokerx-api  | //' | grep --line-buffered '^{' | jq --unbuffered -Cr 'select(.["@mt"]) | "\(.["@t"][11:19]) | \(.["@mt"] | split(" - ")[0])\(if .Email then " | \(.Email)" else "" end)\(if .ClientId then " | \(.ClientId[0:8])" else "" end)"'
