#!/bin/bash
# 1. Recupera il nome del cliente passato come parametro ($1)
CLIENTE=$1

if [ -z "$CLIENTE" ]; then
    echo "Errore: Specifica il cliente! Es: ./deploy.sh cliente-a"
    exit 1
fi

# 2. Crea la cartella destinazione e copia gli asset del cliente
mkdir -p core/custom_assets/
if [ -d "config-clienti/$CLIENTE" ]; then
    cp -r config-clienti/$CLIENTE/* core/custom_assets/
    echo "✓ Deploy completato con successo per $CLIENTE!"
else
    echo "Errore: Cartella config-clienti/$CLIENTE non trovata!"
    exit 1
fi