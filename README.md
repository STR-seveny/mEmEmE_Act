# mEmEmE(Act)

一个 **osu!framework** 下落式节奏游戏，逻辑由早期的 Scratch（TurboWarp）版本 **Rx** 移植而来。

*A falling-note rhythm game built on **osu!framework**, ported from an earlier Scratch (TurboWarp) version called **Rx**.*

---

## 目录 / Table of Contents

- [当前状态 / Status](#当前状态--status)
- [操作 / Controls](#操作--controls)
- [音符类型 / Note Types](#音符类型--note-types)
- [特殊机制 / Gameplay Mechanics](#特殊机制--gameplay-mechanics)
- [谱面格式 / Chart Format](#谱面格式--chart-format)
- [编译运行 / Building & Running](#编译运行--building--running)
- [第三方依赖 / Third-Party Dependencies](#第三方依赖--third-party-dependencies)
- [许可 / License](#许可--license)

---

## 当前状态 / Status

**`beta0.0.2`** —— 从启动到进关是一条完整流程：主菜单 → 选曲 → 游戏。

| 已完成 / Done | 未实现 / Not yet implemented |
| --- | --- |
| 四种音符判定（dE / tE / nE / bug） | `effect` 演出特效 |
| 逐像素子步进判定（照 Rx 的做法） | `camera` 镜头移动 |
| 命中特效（Rx 素材） | `end` 结束流程 |
| REV / SPT 两项机制 | 结算界面、成绩统计 |
| 音频时钟同步 | |
| 谱面解析（`.me3`） | |
| 主菜单与选曲界面 | |
| `.me4` 关卡包（谱面 + 音频 + 曲绘 + 元数据） | |

*The flow from launch to gameplay is complete: main menu, song select and `.me4` level packages.
Effects (`effect`), camera movement (`camera`), and the end sequence (`end`) are still stubs.*

---

## 操作 / Controls

| 操作 / Input | 作用 / Action |
| --- | --- |
| 移动鼠标 / Move mouse | 控制接收器左右移动 / Move the receptor |
| 鼠标左键 / Left mouse button | tE 点击判定 / Judge a `tE` |
| `Z` / `X` | 同上（键盘替代）/ Same as above |
| 鼠标左键（主菜单）/ Left click (menu) | 展开面板 / 开始关卡 / 收起面板 · Open, play, collapse |
| `ESC` | 游戏中返回主菜单 / Back to the main menu |

（`Z` / `X` 会忽略系统按键连发，避免按住不放被当成连续点击。）

---

## 音符类型 / Note Types

音符从屏幕上方落下，判定区在下方（**黄条在上、蓝条在下**）。

*Notes fall from the top; the judgement zone sits near the bottom (**yellow band above, blue band below**).*

### `dE` — 接住型 / Catch

- 接收器碰到黄条**或**蓝条 → **Good**
- 一直没接住、落到 **missLine**（底部红线）→ **Miss**
- 不需要点击，自动判定

*Receptor touches either band → **Good**. Falls to the missLine uncaught → **Miss**. No click required.*

### `tE` — 点击型 / Tap

- 在判定窗口内**点击**（左键 / `Z` / `X`）：
  - 压在**黄条**时点击 → **Good**
  - 压在**蓝条**时点击 → **Bad**
- 没点、落到 missLine → **Miss**
- 判定窗口 = 判定区上下各放宽 25 像素

*Click while inside the hit window: **yellow** → Good, **blue** → Bad. Never clicked → **Miss**.*

### `nE` — 反向型 / Inverted

- 接收器碰上**黄条** → **Miss**
- 接收器碰上**蓝条** → **Bad**
- 全程没碰上、落到 missLine → **Good**

规则和 dE 相反：**躲开它才对**。因为它的"命中点"在 missLine（比判定区更靠下），它会比同时间的其他音符**更早进入画面**。

*The inverse of `dE`: touching it is bad, letting it fall through is good. Its hit point is the missLine, so it enters the screen earlier than other notes.*

### `bug` — 触发型 / Trigger

不参与判定，**Y 坐标到达判定区时立刻触发效果并消失**，接不接都一样。

*Not judged at all — triggers its effect the moment it reaches the judgement zone, regardless of the receptor.*

| 类型 / Type | 效果 / Effect |
| --- | --- |
| `REV` | 反转鼠标 X（接收器、`me_x`、点击位置全都镜像）/ Mirror mouse X |
| `SPT` | 接收器分成两半（主体 + 镜像分体），各 75 宽 / Split the receptor into two halves |

（其他名字（如 `XXX`）不会触发任何效果，音符照常下落但不显示字样——**"没有字的色块"本身就是提示：这个 bug 类型写错了**。）

---

## 特殊机制 / Gameplay Mechanics

### REV — 镜像 / Mirror

开启后，**真实鼠标**经一次镜像得到**逻辑鼠标**：

```
逻辑鼠标.X = 屏幕宽 - 真实鼠标.X
```

接收器位置、谱面参数 `me_x`、以及 tE 的点击位置**全部使用逻辑鼠标**，保证"看到的位置"和"判定的位置"永远一致。再触发一次即关闭。

*While active, the mouse X is mirrored to produce a "logical mouse" that drives the receptor, `me_x`, and click positions alike. Trigger again to toggle off.*

### SPT — 分体 / Split

接收器分成两块，**每块宽度减半**（150 → 75）：

- **主体**跟着逻辑鼠标
- **分体**是主体相对**屏幕中线**的镜像（鼠标在正中时两块重合，越偏越开）

判定上：音符碰到**任意一块**都算，取最近的那半；**tE 则是"点一下、两块各判一条"**——因为分体在屏幕另一边、鼠标只有一个位置，玩家不可能分别去点两边。

*The receptor splits into two 75-wide halves: the main half follows the logical mouse, the mirror half is its reflection across the screen centre. A single click judges one `tE` in **each** half.*

### 两者叠加 / REV + SPT together

`REV` 先镜像鼠标，`SPT` 再基于镜像后的位置分两半。因此同时生效时：

**主体反着走，分体反而跟着鼠标**（双重镜像 = 回到真实位置）。

这是有意的设计——**玩家要同时逆向思考两边，本身就是难度来源**（与 Rx 行为一致）。

*With both active the main half moves opposite to the mouse while the mirror half follows it. This is intentional: it raises the difficulty, matching the original Rx behaviour.*

### 判定实现 / How judging works

照搬 Rx 的做法——**逐像素子步进**：

1. 把这一帧音符要走的路程拆成 **1 像素一步**（卡顿时按上限均分，步长变粗但**最后一步必定落在真实位置**）
2. 每一步都停下来问一次"该判了吗"
3. 步长被拉粗时（卡顿），用**扫掠检测**按轨迹补判一次兜底

这样无论帧率怎么抖、音符多快，都**不可能一步跳过判定区**。

*Sub-stepping: each frame's travel is split into 1-pixel steps, judging at every step (with a swept-trajectory fallback when a frame hitch coarsens the steps). A note can never skip over the judgement zone.*

---

## 谱面格式 / Chart Format

扩展名 `.me3`，纯文本，一行一条指令，`//` 开头为注释：

```
play(0.9,0)               // 0.9 秒时开始播放音频，从音频 0 秒处起
speed(0,10)               // 0 秒时把下落速度设为 10
note(1.5,dE,0,0)          // 1.5 秒时生成 dE 音符，X 参数 0，id 0
note(1.9,tE,120,1)        // tE，X 参数 120，id 1
bug(24.28,SPT,0,0)        // 24.28 秒时生成 SPT 触发块
```

| 指令 / Command | 参数 / Arguments |
| --- | --- |
| `play(t, start)` | 播放音频，`start` 为音频起点（秒） |
| `speed(t, v)` | 设置下落速度（v 越大越快） |
| `note(t, type, x, id)` | 生成音符，`type` = `dE` / `tE` / `nE` |
| `bug(t, type, x, id)` | 生成触发块，`type` = `REV` / `SPT` |
| `move(t, id, x, y)` | 移动对象 |
| `effect(t, type, size)` | 演出特效（**未实现**） |
| `camera(t, x, y)` | 镜头移动（**未实现**） |
| `end(t)` | 结束（**未实现**） |
| `var(t, name, value)` | 设置变量 |

**`x` 参数**支持数值与表达式：

| 写法 / Form | 含义 / Meaning |
| --- | --- |
| `120` | 数值 |
| `me_x` / `me_y` | 鼠标位置（受 REV 影响） |
| `var.名字` | 变量值 |
| `id_my_x` / `id_my_y` | 某个对象的当前坐标 |
| `random(最小/最大)` | 随机数 |

### `.me4` 关卡包 / Level package

`.me4` 是**改了后缀名的 zip**，一个文件装下一个关卡的全部内容：

| 文件 / File | 必需 / Required | 内容 / Contents |
| --- | --- | --- |
| `chart.me3` | ✅ | 谱面，语法同上 / The chart, same syntax as above |
| `music.wav` / `.mp3` / `.ogg` | | 音轨，按这个顺序取第一个存在的 / First one that exists wins |
| `art.png` | | 曲绘（方形）/ Square cover art |
| `data.txt` | | 元数据四行：标题 / 艺术家 / 谱师 / 曲绘师 |

放进 `Resources/Levels/`，启动时自动扫描并出现在选曲列表里。

*A `.me4` is a **zip with a renamed extension** holding one whole level: `chart.me3`, optional
`music.*` and `art.png`, plus `data.txt` with four lines of metadata. Drop it in
`Resources/Levels/` and it appears in the song list at launch.*

格式细节见 `Resources/Levels/README.md`。

---

## 编译运行 / Building & Running

**依赖 / Requirements**：.NET 8 SDK

### ⚠️ 构建前必读：需要自备 Six Labors 授权文件

本项目使用 `SixLabors.ImageSharp.Drawing`，它的构建目标会**强制校验许可证**——**仓库里没有、也不会有 `sixlabors.lic`**（那是私人凭证，见下方许可说明）。

因此**克隆后直接编译会失败**，报错：

```
error : No Six Labors license found. Set $(SixLaborsLicenseKey),
        set $(SixLaborsLicenseFile), or add a 'sixlabors.lic' file to the project/workspace.
```

**三种解决方式（任选其一）**：

1. **申请自己的授权**（推荐）——到 <https://sixlabors.com/pricing/> 获取 Community / 商业授权，把拿到的 `sixlabors.lic` 放到 `mEmEmE_Act.Game/` 目录下
2. **用环境变量 / MSBuild 属性**（不落地文件）：
   ```bash
   dotnet build mEmEmE_Act.Desktop/mEmEmE_Act.Desktop.csproj \
     -p:SixLaborsLicenseKey="<你的密钥>"
   ```
3. **移除该依赖**（若你不需要运行时文字渲染）——删掉 `mEmEmE_Act.Game.csproj` 里的
   `<PackageReference Include="SixLabors.ImageSharp.Drawing" />`，
   并改写 `BugNote.cs` 里渲染 `REV` / `SPT` 字样的代码

> 项目自身的**开源发布不包含任何授权凭证**；`sixlabors.lic` 已在 `.gitignore` 中排除。

### ⚠️ 关卡与音频不包含在仓库中 / Levels and audio are not shipped

**关卡和音乐都没有随仓库发布**——它们属于内容资产，且音乐只有"使用授权"、不可公开再分发。

克隆后请自备关卡包放进 `mEmEmE_Act.Game/Resources/Levels/`：

*Neither levels nor music are shipped in this repository — they are content assets, and the music's licence permits use only.*

*Supply your own level package in `mEmEmE_Act.Game/Resources/Levels/`:*

```
YourLevel.me4      ← 一个文件装下 谱面 + 音频 + 曲绘 + 元数据
```

`.me4` 是**改了后缀名的 zip 压缩包**，格式说明见 `Resources/Levels/README.md`。

*A `.me4` is a **zip archive with a renamed extension**; see `Resources/Levels/README.md` for the format.*

**没有关卡时游戏会正常启动**，选曲列表显示一条提示，无法进入关卡。

*Without any level the game still launches; the song list is simply empty.*

> **关卡只在启动时扫描一次** —— 增删 `.me4` 之后需要重启游戏。
> *Levels are scanned once, at launch — restart the game after adding or removing a `.me4`.*
>
> 关卡包会被解包到 `%LOCALAPPDATA%\mEmEmE_Act\cache\`，缓存目录按「文件名 + 时间戳」命名，
> 所以同名关卡换一份新的会重新解包，旧缓存不会自动清理。
> *Packages are extracted into `%LOCALAPPDATA%\mEmEmE_Act\cache\`; the cache folder is keyed by
> file name plus timestamp, so replacing a level re-extracts it and leaves the old cache behind.*

### 编译 / Build

```bash
git clone <仓库地址>
cd mEmEmE_Act
dotnet build mEmEmE_Act.Desktop/mEmEmE_Act.Desktop.csproj
dotnet run --project mEmEmE_Act.Desktop/mEmEmE_Act.Desktop.csproj
```

或直接运行编译产物：
`mEmEmE_Act.Desktop/bin/Debug/net8.0/mEmEmE_Act.exe`

**目前只支持 Desktop**（iOS 项目存在但未验证）。

*Desktop only for now (an iOS project exists but is untested).*

### 项目结构 / Project Layout

```
mEmEmE_Act.Game/
  MainMenuScreen.cs        主菜单 = 选曲界面（波形 + logo + 可上滑的 START 面板、关卡列表）
  WaveformDisplay.cs       顶部波形，跟随菜单曲频谱跳动（同时持有菜单曲）
  GameplayScreen.cs        游戏屏幕：输入、REV / SPT 的位置计算
  PrepareOverlay.cs        进关前的准备遮罩
  Recepter.cs              接收器（判定区几何量）
  Note.cs                  音符基类(=dE) / NoteTE / NoteNE，各自实现判定
  BugNote.cs               REV / SPT 触发块
  HitEffect.cs             命中特效
  DynamicText.cs           运行时文字渲染（SixLabors）
  Charts/Playfield.cs      判定主循环（子步进推进、反馈、谱面事件调度）
  Charts/ChartParser.cs    .me3 谱面解析
  Charts/Me4Package.cs     .me4 关卡包读取与解包
  Charts/LevelLibrary.cs   扫描 Resources/Levels 里的关卡
```

---

## 第三方依赖 / Third-Party Dependencies

本项目的**源代码**以 MIT 许可发布，但**下列第三方组件各有自己的许可**，不受本项目 MIT 许可覆盖。使用、分发或修改本项目时请一并遵守。

*This project's **source code** is MIT-licensed, but the third-party components below carry their own licences and are **not** covered by it.*

### ppy.osu.Framework `2025.1021.0`

游戏引擎。[MIT License](https://github.com/ppy/osu-framework/blob/master/LICENCE) — © ppy Pty Ltd

### SixLabors.ImageSharp.Drawing `3.0.0`

用于在运行时渲染 **bug 音符的字样纹理**（`REV` / `SPT`）。

> **重要：Six Labors 采用双许可（Split License）**
>
> - **开源 / 非商业用途**：可按 [Apache 2.0](https://www.apache.org/licenses/LICENSE-2.0) 使用
> - **商业用途**：需要购买 [Six Labors 商业授权](https://sixlabors.com/pricing/)
>
> 本项目通过 **Six Labors Community License** 使用该组件（授权文件 `sixlabors.lic` 属于**私有凭证，不包含在本仓库中**）。如需自行构建并在商业场景使用，请自行确认授权。
>
> 这是**依赖自身的许可条款**，本项目无法代为授予任何权利。

*ImageSharp.Drawing is used to rasterise the `REV`/`SPT` label textures at runtime. Six Labors licenses it under a **split licence**: Apache 2.0 for open-source/non-commercial use, or a paid commercial licence otherwise. The `sixlabors.lic` file is a **private credential and is deliberately not included in this repository**.*

### FontStashSharp `1.6.1` · FontStashSharp.Rasterizers.StbTrueTypeSharp `1.2.9`

字体光栅化（osu!framework 的传递依赖）。[MIT License](https://github.com/FontStashSharp/FontStashSharp/blob/main/LICENSE)

### NUnit / Microsoft.NET.Test.Sdk（仅测试项目）

测试框架，仅在 `mEmEmE_Act.Game.Tests` 中使用。

### 字体 / Font

`Resources/Fonts/SourceHanSansSC-Regular.otf` — **思源黑体 / Source Han Sans**
[SIL Open Font License 1.1](https://github.com/adobe-fonts/source-han-sans/blob/master/LICENSE.txt) — © Adobe

### 音频与美术素材 / Audio & Art Assets

| 资源 | 是否在仓库中 | 说明 |
| --- | --- | --- |
| `Resources/Audio/1_1_1.mp3` | ❌ **不在** | 授权仅限使用，不可公开再分发；需自备 |
| `Resources/Sound/menu.mp3` | ❌ **不在** | 主菜单曲（6 MB）；需自备，缺失时波形回落成静态形状 |
| `Resources/Sound/hit.mp3` | ✅ 在 | 打击音效（Scratch 无版权音效库混音） |
| `Resources/Textures/logo.png`、`START.png` | ✅ 在 | 主菜单 logo 与 START 面板素材 |
| `Resources/Effects/*.png` | ✅ 在 | 命中特效素材（Rx） |

其余音频与图片素材的版权归其各自作者所有，**不适用本项目的 MIT 许可**。

*The background music is deliberately excluded (see above). Other audio and image assets remain the property of their respective authors and are not covered by this project's MIT licence.*

---

## 许可 / License

本项目源代码采用 **MIT License**，详见 [LICENSE](LICENSE)。

*Source code is released under the **MIT License** — see [LICENSE](LICENSE).*

第三方组件与素材的许可见上一节；**MIT 许可不适用于它们**。

*Third-party components and assets are licensed separately; the MIT licence does not apply to them.*

---

© 2026 STRstudio2175
