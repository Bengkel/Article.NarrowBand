

## ESP WROOM 32
![](./images/esp-wroom-32.webp)

[Technical Information](https://www.espressif.com/sites/default/files/documentation/esp32-wroom-32e_esp32-wroom-32ue_datasheet_en.pdf) 

## M5 STAMP Cat-M

![](./images/m5stack-catm.webp)

[Technical Information](https://docs.m5stack.com/en/stamp/stamp_catm)

## nanoFramework

[![](https://i0.wp.com/www.nanoframework.net/wp-content/uploads/B.nanoframework_logocolor500-1.png?resize=300%2C153&ssl=1)](https://www.nanoframework.net/)

Install nanoFramework .NET Core Tool [nano firmware flasher](https://github.com/nanoframework/nanoFirmwareFlasher) through command line interface

```
dotnet tool install -g nanoff
```

Flash your device

```
nanoff --platform esp32 --update --serialport {yout-serial-port}
```

For example

```
nanoff --platform esp32 --update --serialport COM6
```

## PuTTY

![Session settings](./images/putty-session.webp)
![Serial settings](./images/putty-serial.webp)

[Download PuTTY](https://www.putty.org/)

## Azure CLI /IoT extensions

Install the [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) and the [IoT Extension](https://github.com/Azure/azure-iot-cli-extension) 

```
az iot hub monitor-events -na-b ot-weu-iot --properties anno sys --timeout 0
```

Requirement notice

![](./images/uamqp-dependency.webp)