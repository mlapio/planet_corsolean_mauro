<?php
$input = $_GET['user']; // 1. Source (Tainted)

$sql = "SELECT * WHERE name=" . $input; // 2. Propagazione

pg_query($db, $sql); // 3. Sink (Vulnerable!)
?>