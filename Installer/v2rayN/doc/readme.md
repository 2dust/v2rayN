# V2Ray 内核

V2Ray 内核可以单独使用，也可以配置其它程序一起使用。

官网：https://www.v2ray.com/

## 使用方式

### Windows 或 macOS

压缩包内的 config.json 是默认的配置文件，无需修改即可使用。配置文件的详细信息可以在官网找到。

* Windows 中的可执行文件为 v2ray.exe 和 wv2ray.exe。双击即可运行。
  * v2ray.exe 是一个命令行程序，启动后可以看到命令行界面。
  * wv2ray.exe 是一个后台程序，没有界面，会在后台自动运行。
* macOS 中的可执行文件为 v2ray。右键单击，然后选择使用 Terminal 打开即可。

### Linux

压缩包中包含多个配置文件，按需使用。

可执行程序为 v2ray，启动命令：

```bash
v2ray --config=<full path>
```

## 验证文件

压缩包中的 .sig 文件为 GPG 签名文件，用来验证对应程序文件的真实性。签名公钥可以在下面的链接找到：

https://github.com/v2ray/v2ray-core/blob/master/release/verify/official_release.asc

## 问题反馈

* Github: https://github.com/v2ray/v2ray-core
* Telegram: https://t.me/v2fly_chat
