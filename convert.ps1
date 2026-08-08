Add-Type -AssemblyName System.Drawing
$icon = New-Object System.Drawing.Icon("monster.ico")
$bmp = $icon.ToBitmap()
$bmp.Save("mobile-app\assets\images\icon.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$icon.Dispose()
