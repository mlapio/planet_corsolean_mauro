<?php
// input non sanificato concatenato direttamente nella query
$userId = $_GET['id'];
$sql = "SELECT * FROM teatri WHERE id = " . $userId;
$result = pg_query($dbconn, $sql); // Semgrep rileva SQL Injection qui!
?>