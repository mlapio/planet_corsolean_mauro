<?php
// 1. Lettura bootstrap da ambiente (Zero Hardcoding)
$vaultAddr = getenv('VAULT_ADDR') ?: 'http://localhost:8200';
$vaultToken = getenv('VAULT_TOKEN') ?: 'myroot';

// 2. Context HTTP per l'API REST di Vault
$opts = [
    'http' => [
        'method' => 'GET',
        'header' => "X-Vault-Token: {$vaultToken}\r\n"
    ]
];

// 3. Esecuzione chiamata REST e decodifica JSON
$url = "{$vaultAddr}/v1/secret/data/planet/db";
$json = file_get_contents($url, false, stream_context_create($opts));
$res = json_decode($json, true);

// 4. Password caricata solo in RAM & Connessione PDO
$dbPass = $res['data']['data']['password'] ?? null;
$pdo = new PDO("pgsql:host=localhost;dbname=misuratore", "postgres", $dbPass);
var_dump($dbPass, $pdo);