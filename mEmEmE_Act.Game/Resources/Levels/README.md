# Levels

关卡放在这里，格式为 `.me4`。

Levels live here, in `.me4` format.

## 什么是 .me4 / What is a .me4

`.me4` 就是一个**改了后缀名的 zip 压缩包**，一个文件装下整个关卡：

*A `.me4` is simply a **zip archive with a renamed extension** — one file holding a whole level:*

```
YourLevel.me4  (zip)
 ├── chart.me3    谱面（与 .me3 语法完全相同）/ the chart (identical to .me3 syntax)
 ├── music.wav    音频（.mp3 / .ogg 也行）/ the track (.mp3 / .ogg also accepted)
 ├── art.png      曲绘，建议正方形 / cover art, square recommended
 └── data.txt     元数据，四行纯文本 / metadata, four plain-text lines
```

**`data.txt` 四行依次是 / the four lines of `data.txt` are, in order:**

```
曲名      title
曲师      artist
谱师      charter
曲绘画师  illustrator
```

严格四行，**允许缺行（留空行）**。/ Exactly four lines; missing values may be left blank.

## 关于仓库 / About this repository

**本目录里没有 `.me4` 文件**——关卡属于内容资产，不随仓库发布（`.gitignore` 已排除）。

*No `.me4` files are shipped in this repository — levels are content assets and are excluded via `.gitignore`.*

要试玩，把自己做的或拿到的 `.me4` 放进这个目录（编译输出目录下的 `Resources\Levels\`），启动游戏即可。

*To play, drop a `.me4` into this folder (in the build output: `Resources\Levels\`) and launch the game.*

> 目前**只会加载找到的第一个 `.me4`**，还没有选曲界面。
> *For now only the **first `.me4` found** is loaded; there is no song select screen yet.*

## 打包 / Packaging

把上面四个文件放进一个 zip，改后缀为 `.me4` 即可。PowerShell 示例：

*Zip the four files and rename the extension to `.me4`. PowerShell example:*

```powershell
Compress-Archive -Path chart.me3, music.wav, art.png, data.txt -DestinationPath YourLevel.zip
Rename-Item YourLevel.zip YourLevel.me4
```

谱面格式（指令、`x` 参数表达式）见 `../Charts/README.md`。

*For the chart format (commands, `x` parameter expressions) see `../Charts/README.md`.*
