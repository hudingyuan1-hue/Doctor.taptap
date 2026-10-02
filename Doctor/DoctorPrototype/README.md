# Doctor 第一场景验证原型

打开 `D:\Doctor\DoctorPrototype`，使用本机 Unity 6.3 LTS `6000.3.16f1`。设计参考版本 `6000.3.25f1` 已由官方发布；本机未安装该补丁版。本工程没有改用 Unity 6.4 或 2022。

当前验证边界：C# 独立规则验证和对本机 Unity 程序集的编译已完成；Unity 编辑器因缺少有效许可证不能启动，场景导入、实际画面、物理暂停和 Windows 构建仍未验证。不要把独立验证结果当成 Unity Play Mode 结果。

## 启动

1. 在 Unity Hub 登录并激活可用许可证。
2. Hub 添加本目录，选择本机 `6000.3.16f1`，或运行上级目录 `Launch-Doctor.ps1`。
3. 打开 `Assets/Doctor/Scenes/FirstRoom.unity`，按 Play。也可用菜单 `Doctor > Open First Room`。
4. 想导出时用 `Doctor > Build Windows Prototype`。构建目标为 `Build/Doctor.exe`；当前未产生或声称通过构建。

## 操作

- 左键点击浅蓝睡衣角色，打开已拥有操控点；按钮施放神经元。可以在同一菜单里反复施放。
- 右键打开能力图，消耗神经簇解锁肠胃蠕动或升级强度。
- Esc 打开/关闭菜单。所有菜单暂停世界、概率和物理；施放的资源交易立即记录，人物身体反应与攻击在继续后执行。
- 不提供直接移动控制。角色沿房间中央走道自主移动到真实三维家具前。
- 顶部显示周目、搜索计数、资源和状态；底部说明这次施放影响的行动。
- F3 显示开发结算记录。普通界面在第一次觉醒前不披露数值抗性。
- 新测试档在 Esc 菜单中创建，有二次确认。第一场景离开或精神≤0后停在当前范围出口，不虚构后续胜负。

## 数值和复现

运行时数值统一读取 `Assets/Doctor/Resources/TestParameters.json`，停止 Play 后修改。已确定的每轮10次搜索、每个实际周目系统补充1+1、死亡周目+3不暴露为平衡滑杆。

初始测试档已有皮肤触觉，额外起始资源为0，第一次周目系统给予1神经元、1神经簇。皮肤初始强度2、抗性3，每次费用1；先让角色完成搜索积攒神经元，再于同一行动前施放两次，即可观察从抵抗到生效。不要把这些平衡参数当成设计定值。

用于复现的参数：`randomSeed`（0使用时间种子）、`forceCatAtRoundStart`、`forceMotherAtBoundary`、`suppressCat`、`suppressMother`。默认不强制人物。母亲在行动边界出现一次；猫在新周目开始就可见。强制参数仍走同一组规则，不调用专用连锁。

低精神休息打断惩罚明确标记为 `TEST_AddExtraGrowth_NotDoubleExisting`，本原型测试额外增长量 `interruptedRestExtraGrowth`，没有把“翻倍已有抗性”或“翻倍增长量”当成定案。

存档位置为 Windows 用户 LocalLow 下 `DoctorPrototype/Doctor First Room/first-room-v1.json`。菜单打开、施放/升级与退出时保存，保留 `.bak`。保存完整行动阶段、随机数状态和待执行身体反应；读档不会额外赠送周目资源。环境损伤以固定家具编号0—9保存，家具主体不移动，碎片不能阻路或掉钥匙。

## 验证入口

上级 `scripts/Verify-Doctor.ps1` 运行独立规则测试、Unity API 引用编译、场景引用静态检查。加 `-Unity` 执行编辑器场景检查，加 `-Build` 执行真实构建；后两项需要许可证。

构建后启动 `Doctor.exe -doctorValidate` 可执行真实引擎时钟/刚体暂停与恢复探针并输出截图和结果。验证模式不读写用户测试存档。当前许可证阻断期间没有这份运行结果。

第一次试玩重点观察：能否看懂当前施放影响哪次行动；被抵抗与累计突破是否可区分；是否会根据猫的位置与母亲的条件改变干预时机。
