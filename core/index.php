<?php

$config = json_decode(file_get_contents("C:\PWeb\Work\!corso_lean\!repos\planet_corsolean_mauro\core\custom_assets\config.json"));
?>

<html>

<head></head>

<body>
    <div>
        <?php echo $config->enable_vip_catering ?? false ? "⭐ Servizio VIP Catering Attivo per La Fenice" : ""; ?>
    </div>
    <img style="width: 200px" src="custom_assets/logo.png">
</body>

</html>