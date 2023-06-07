CurseTheBeast
==============

**This project is only for areas where the FTB APP does not work well due to the network environment. Please do not propose i18n support.**

## 介绍
一款轻量级FTB整合包下载工具。有以下特性：

- 打包为Curseforge整合包格式，主流启动器都能导入安装。
- 支持下载安装服务端，方便开服。
- 自动检测系统代理和HTTP_PROXY环境变量，也可以通过命令行参数指定代理。
- 从多个镜像源下载依赖，比官方安装器更快。（检测到代理时不使用镜像源）

## 使用说明

两种使用方式

- **双击打开，按照引导操作**

  ![](doc/img/double_click1.png)
  
  ![](doc/img/double_click2.png)
  
  ![](doc/img/double_click3.png)
  
  ![](doc/img/double_click4.png)

- **命令行**

  ![](doc/img/commandline1.png)

  ![](doc/img/commandline2.png)
  
## 整合包安装方法

把打包好的文件拖到启动器上即可安装

![](doc/img/installation.jpg)

## 常见问题
- 整合包文件非常多时，获取清单会很慢（如FTB Interactions）。开启代理即可。
- 偶尔会有文件下载失败，自动重试三次后报错崩溃。一般只要重开就能解决，开启代理更好。
- 服务端包含了一些客户端的文件？说明FTB官方没有把这些文件标记为ClientOnly，用官方下载器一样会下载到这些。（一般不影响使用）
- 有些mod作者删库跑路，导致部分文件无法下载，需手动处理。请留意控制台输出的信息和包里的“unreachable-files.json”文件。
- 1.12.2之前的版本年久失修，本工具仅保证正确下载安装，其它问题需自行解决。

## 镜像源

| 名称     | 用途     |
| :------:  | :------ |
| [MCBBS](https://www.mcbbs.net/) | MC meta/jar/maven、Forge/Fabric maven |
| [BMCLAPI](https://bmclapidoc.bangbang93.com/) | MC meta/jar/maven、Forge/Fabric maven |
| [LSS233](https://www.mcbbs.net/forum.php?mod=viewthread&tid=800729) | MC/Forge/Fabric maven |

## 缓存

缓存能加速程序运行，但时间久了会占用大量存储空间，可以定期手动清理。

windows下缓存位置为
`%LOCALAPPDATA%\CurseTheBeast`

linux下为
`~/.local/share/CurseTheBeast`

## 其它

默认UA
`CurseTheBeast/0.0`